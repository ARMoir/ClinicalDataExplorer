using ClinicalDataExplorer.Models;
using System.Xml;

namespace ClinicalDataExplorer.Services;

public sealed partial class FhirService
{
    public static readonly TimeSpan PatientEnrichmentBudget = TimeSpan.FromSeconds(20);

    // Best-effort, sequential enrichment. Completed snapshots remain with the caller on failure.
    public async Task<bool> EnrichPatientResourcesAsync(string patientId,
        Func<IReadOnlyList<PatientResourceSection>, Task> onProgress,
        CancellationToken cancellationToken = default,
        Func<CancellationToken, Task>? waitForForeground = null,
        SemaphoreSlim? requestSlots = null,
        TimeSpan? timeBudget = null)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeBudget ?? PatientEnrichmentBudget);
        var token = budget.Token;
        try
        {
            await using var pages = StreamPatientResourceSectionsAsync(patientId, token).GetAsyncEnumerator(token);
            while (true)
            {
                if (waitForForeground is not null) await waitForForeground(token);
                if (requestSlots is not null) await requestSlots.WaitAsync(token);
                bool received;
                try { received = await pages.MoveNextAsync(); }
                finally { requestSlots?.Release(); }
                token.ThrowIfCancellationRequested();
                if (!received) return true;
                await onProgress(pages.Current.Sections);
                if (!pages.Current.HasMore) return true;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or XmlException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
    }
}
