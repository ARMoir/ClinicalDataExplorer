<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:f="http://hl7.org/fhir"
    xmlns:p="urn:clinical-data-explorer:presentation"
    exclude-result-prefixes="f p">

  <xsl:output method="html" omit-xml-declaration="yes" encoding="UTF-8" />

  <!-- Application branding is supplied at transform time by XsltService. -->
  <xsl:param name="facilityName" select="'Your Facility'" />
  <xsl:param name="logoPath" select="''" />
  <xsl:param name="primaryColor" select="'#1F618D'" />
  <xsl:param name="secondaryColor" select="'#17202A'" />

  <xsl:variable name="upper" select="'ABCDEFGHIJKLMNOPQRSTUVWXYZ'" />
  <xsl:variable name="lower" select="'abcdefghijklmnopqrstuvwxyz'" />

  <xsl:template match="/">
    <xsl:choose>
      <xsl:when test="f:Bundle">
        <xsl:apply-templates select="f:Bundle" />
      </xsl:when>
      <xsl:otherwise>
        <div class="fhir-empty">The server did not return a valid report response.</div>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template match="f:Bundle">
    <xsl:variable name="report" select="f:entry/f:resource/f:DiagnosticReport[1]" />
    <xsl:variable name="observations" select="f:entry/f:resource/f:Observation" />
    <!-- Treat a long or multi-line valueString as a narrative/document rather than a table result. -->
    <xsl:variable name="narratives" select="$observations[f:valueString/@value and (contains(f:valueString/@value, '&#10;') or contains(f:valueString/@value, '\n') or string-length(f:valueString/@value) &gt; 500)]" />
    <xsl:variable name="results" select="$observations[not(f:valueString/@value and (contains(f:valueString/@value, '&#10;') or contains(f:valueString/@value, '\n') or string-length(f:valueString/@value) &gt; 500))]" />

    <div class="fhir-report" style="--primary-color:{$primaryColor}; --secondary-color:{$secondaryColor};">
      <xsl:choose>
        <xsl:when test="$report">
          <xsl:if test="normalize-space($facilityName) != '' or normalize-space($logoPath) != ''">
            <div class="report-branding">
              <xsl:if test="normalize-space($logoPath) != ''">
                <img class="report-brand-logo" src="{$logoPath}" alt="" />
              </xsl:if>
              <xsl:if test="normalize-space($facilityName) != ''">
                <div class="report-facility-name"><xsl:value-of select="$facilityName" /></div>
              </xsl:if>
            </div>
          </xsl:if>

          <div class="report-title-row">
            <div>
              <div class="eyebrow">DIAGNOSTIC REPORT</div>
              <h2>
                <xsl:choose>
                  <xsl:when test="$report/f:code/f:text/@value"><xsl:value-of select="$report/f:code/f:text/@value" /></xsl:when>
                  <xsl:when test="$report/f:code/f:coding/f:display/@value"><xsl:value-of select="$report/f:code/f:coding/f:display/@value" /></xsl:when>
                  <xsl:otherwise>Diagnostic Report</xsl:otherwise>
                </xsl:choose>
              </h2>
            </div>
            <div class="status-pill"><xsl:value-of select="$report/f:status/@value" /></div>
          </div>

          <div class="report-grid">
            <div class="field"><span class="label">Record ID</span><span class="value"><xsl:value-of select="$report/f:id/@value" /></span></div>
            <div class="field"><span class="label">Identifier</span><span class="value"><xsl:value-of select="$report/f:identifier[1]/f:value/@value" /></span></div>
            <div class="field"><span class="label">Patient</span><span class="value"><xsl:apply-templates select="$report/f:subject/f:reference" mode="report-link" /></span></div>
            <div class="field"><span class="label">Encounter</span><span class="value"><xsl:apply-templates select="$report/f:encounter/f:reference" mode="report-link" /></span></div>
            <div class="field">
              <span class="label">Effective</span>
              <span class="value">
                <xsl:choose>
                  <xsl:when test="$report/f:effectiveDateTime/@value"><xsl:value-of select="$report/f:effectiveDateTime/@value" /></xsl:when>
                  <xsl:when test="$report/f:effectivePeriod/f:start/@value">
                    <xsl:value-of select="$report/f:effectivePeriod/f:start/@value" />
                    <xsl:if test="$report/f:effectivePeriod/f:end/@value"><xsl:text> - </xsl:text><xsl:value-of select="$report/f:effectivePeriod/f:end/@value" /></xsl:if>
                  </xsl:when>
                </xsl:choose>
              </span>
            </div>
            <div class="field"><span class="label">Issued</span><span class="value"><xsl:value-of select="$report/f:issued/@value" /></span></div>
            <xsl:if test="$report/f:performer"><div class="field"><span class="label">Providers</span><span class="value"><xsl:for-each select="$report"><xsl:call-template name="render-performers"/></xsl:for-each></span></div></xsl:if>
          </div>

          <xsl:if test="$report/f:conclusion/@value">
            <div class="conclusion"><span class="label">Conclusion</span><div><xsl:value-of select="$report/f:conclusion/@value" /></div></div>
          </xsl:if>

          <!-- Long text Observations render as documents. -->
          <xsl:if test="$narratives">
            <div class="section-heading">
              <h3>Narrative Reports</h3>
              <span><xsl:value-of select="count($narratives)" /> document(s)</span>
            </div>
            <xsl:apply-templates select="$narratives" mode="narrative" />
          </xsl:if>

          <!-- Normal scalar Observations stay in the results table. -->
          <xsl:if test="$results">
            <div class="section-heading">
              <h3>Included Results</h3>
              <span><xsl:value-of select="count($results)" /> observation(s)</span>
            </div>
            <div class="table-wrap" tabindex="0" role="region" aria-label="Diagnostic report results">
              <table class="results-table">
                <thead><tr><th scope="col">Test</th><th scope="col">Result</th><th scope="col">Flag</th><th scope="col">Reference Range</th><th scope="col">Observation Date</th><th scope="col">Provider</th><th scope="col">Status</th></tr></thead>
                <tbody><xsl:apply-templates select="$results" mode="result-row" /></tbody>
              </table>
            </div>
          </xsl:if>

          <xsl:if test="not($observations)">
            <div class="fhir-empty">No included Observation resources were returned.</div>
          </xsl:if>

          <details class="references">
            <summary>Related records</summary>
            <ul><xsl:for-each select="$report//f:reference"><li><xsl:apply-templates select="." mode="report-link" /></li></xsl:for-each></ul>
          </details>
        </xsl:when>
        <xsl:otherwise>
          <div class="fhir-empty">The search succeeded, but no diagnostic reports were returned.</div>
        </xsl:otherwise>
      </xsl:choose>
    </div>
  </xsl:template>

  <xsl:template match="f:reference" mode="report-link">
    <xsl:choose><xsl:when test="@p:href"><a class="fhir-reference-link" href="{@p:href}"><xsl:value-of select="@value"/></a></xsl:when><xsl:otherwise><xsl:value-of select="@value"/></xsl:otherwise></xsl:choose>
  </xsl:template>
  <xsl:template match="f:Observation" mode="narrative">
    <section class="narrative-card">
      <div class="narrative-card-header">
        <div>
          <div class="eyebrow">OBSERVATION DOCUMENT</div>
          <h3>
            <xsl:choose>
              <xsl:when test="f:code/f:text/@value"><xsl:value-of select="f:code/f:text/@value" /></xsl:when>
              <xsl:when test="f:code/f:coding/f:display/@value"><xsl:value-of select="f:code/f:coding/f:display/@value" /></xsl:when>
              <xsl:otherwise>Clinical Report</xsl:otherwise>
            </xsl:choose>
          </h3>
          <div class="narrative-meta">
            <xsl:if test="f:code/f:coding/f:code/@value"><span><strong>Code:</strong> <xsl:value-of select="f:code/f:coding/f:code/@value" /></span></xsl:if>
            <xsl:if test="f:identifier[f:system/@value='urn:mrn']/f:value/@value"><span><strong>MRN:</strong> <xsl:value-of select="f:identifier[f:system/@value='urn:mrn'][1]/f:value/@value" /></span></xsl:if>
            <xsl:if test="f:identifier[f:system/@value='urn:account']/f:value/@value"><span><strong>Account:</strong> <xsl:value-of select="f:identifier[f:system/@value='urn:account'][1]/f:value/@value" /></span></xsl:if>
          </div>
        </div>
        <div class="status-pill"><xsl:value-of select="f:status/@value" /></div>
      </div>

      <div class="clinical-document">
        <xsl:call-template name="render-report-lines">
          <xsl:with-param name="text" select="f:valueString/@value" />
        </xsl:call-template>
      </div>

      <xsl:if test="f:note/f:text/@value">
        <div style="margin-top:1rem;padding:0.75rem 0.9rem;border-left:3px solid var(--primary-color);background:rgba(127,127,127,0.055);border-radius:0 0.35rem 0.35rem 0;">
          <div style="font-size:0.75rem;font-weight:700;text-transform:uppercase;letter-spacing:0.04em;opacity:0.7;margin-bottom:0.35rem;">Notes</div>
          <ul style="margin:0;padding-left:1.1rem;">
            <xsl:for-each select="f:note/f:text/@value">
              <li style="margin:0.18rem 0;line-height:1.4;white-space:pre-wrap;"><xsl:value-of select="." /></li>
            </xsl:for-each>
          </ul>
        </div>
      </xsl:if>
    </section>
  </xsl:template>

  <!-- Split on real LF characters. Also accepts literal backslash+n as a fallback. -->
  <xsl:template name="render-report-lines">
    <xsl:param name="text" />
    <xsl:choose>
      <xsl:when test="contains($text, '&#10;')">
        <xsl:call-template name="render-report-line"><xsl:with-param name="line" select="substring-before($text, '&#10;')" /></xsl:call-template>
        <xsl:call-template name="render-report-lines"><xsl:with-param name="text" select="substring-after($text, '&#10;')" /></xsl:call-template>
      </xsl:when>
      <xsl:when test="contains($text, '\n')">
        <xsl:call-template name="render-report-line"><xsl:with-param name="line" select="substring-before($text, '\n')" /></xsl:call-template>
        <xsl:call-template name="render-report-lines"><xsl:with-param name="text" select="substring-after($text, '\n')" /></xsl:call-template>
      </xsl:when>
      <xsl:otherwise>
        <xsl:call-template name="render-report-line"><xsl:with-param name="line" select="$text" /></xsl:call-template>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template name="render-report-line">
    <xsl:param name="line" />
    <xsl:variable name="clean" select="translate($line, '&#13;', '')" />
    <xsl:variable name="trim" select="normalize-space($clean)" />
    <xsl:variable name="before-colon" select="normalize-space(substring-before($trim, ':'))" />
    <xsl:variable name="after-colon" select="normalize-space(substring-after($trim, ':'))" />
    <xsl:variable name="colon-position" select="string-length(substring-before($trim, ':')) + 1" />
    <xsl:variable name="has-letter-before-colon"
                  select="translate($before-colon, concat($upper, $lower), '') != $before-colon" />

    <xsl:choose>
      <!-- Preserve paragraph spacing from the source report. -->
      <xsl:when test="$trim = ''">
        <div class="document-blank">&#160;</div>
      </xsl:when>

      <!-- Generic text separators commonly found in plain-text reports. -->
      <xsl:when test="starts-with($trim, '_____') or starts-with($trim, '-----') or starts-with($trim, '=====')">
        <hr class="document-rule" />
      </xsl:when>

      <!-- A reasonably short all-uppercase line is probably a document heading. -->
      <xsl:when test="string-length($trim) &lt;= 120
                      and $trim = translate($trim, $lower, $upper)
                      and translate($trim, $upper, '') != $trim
                      and not(contains($trim, ':'))">
        <div class="document-heading"><xsl:value-of select="$trim" /></div>
      </xsl:when>

      <!--
        Generic label formatting.
        Highlight the text before the FIRST colon when:
          * the prefix contains at least one letter (avoids treating 09:47 as a label),
          * the colon occurs reasonably near the start of the line,
          * the line is not simply a URL.
      -->
      <xsl:when test="contains($trim, ':')
                      and $colon-position &lt;= 80
                      and $has-letter-before-colon
                      and not(starts-with(translate($trim, $upper, $lower), 'http://'))
                      and not(starts-with(translate($trim, $upper, $lower), 'https://'))">
        <!--
          Some source systems serialize narrative punctuation as \:.  Since
          substring-before(..., ':') leaves that backslash on the label,
          remove exactly one trailing backslash before rendering the label.
          Use real <strong> markup so the emphasis does not depend on CSS.
        -->
        <xsl:choose>
          <!-- A label with no value is treated like a section heading. -->
          <xsl:when test="$after-colon = ''">
            <div class="document-section-heading">
              <strong>
                <xsl:choose>
                  <xsl:when test="substring($before-colon, string-length($before-colon), 1) = '\'">
                    <xsl:value-of select="normalize-space(substring($before-colon, 1, string-length($before-colon) - 1))" />
                  </xsl:when>
                  <xsl:otherwise><xsl:value-of select="$before-colon" /></xsl:otherwise>
                </xsl:choose>
                <xsl:text>:</xsl:text>
              </strong>
            </div>
          </xsl:when>
          <xsl:otherwise>
            <div class="document-labeled-line">
              <strong class="document-label">
                <xsl:choose>
                  <xsl:when test="substring($before-colon, string-length($before-colon), 1) = '\'">
                    <xsl:value-of select="normalize-space(substring($before-colon, 1, string-length($before-colon) - 1))" />
                  </xsl:when>
                  <xsl:otherwise><xsl:value-of select="$before-colon" /></xsl:otherwise>
                </xsl:choose>
                <xsl:text>:</xsl:text>
              </strong>
              <xsl:text> </xsl:text>
              <span class="document-label-value">
                <xsl:value-of select="$after-colon" />
              </span>
            </div>
          </xsl:otherwise>
        </xsl:choose>
      </xsl:when>

      <!-- Everything else is retained as normal narrative text. -->
      <xsl:otherwise>
        <div class="document-line"><xsl:value-of select="$clean" /></div>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <!--
    Resolve Observation.performer references into human-readable names when the
    referenced resources are present in the search Bundle. This is intentionally
    generic: performers may be PractitionerRole, Practitioner, or Organization.
  -->
  <xsl:template name="render-performers">
    <xsl:choose>
      <xsl:when test="f:performer">
        <xsl:for-each select="f:performer">
          <xsl:if test="position() &gt; 1"><br /></xsl:if>
          <xsl:call-template name="render-performer">
            <xsl:with-param name="performer" select="." />
          </xsl:call-template>
        </xsl:for-each>
      </xsl:when>
      <xsl:otherwise>—</xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template name="render-performer">
    <xsl:param name="performer" />
    <xsl:variable name="reference" select="$performer/f:reference/@value" />
    <xsl:variable name="role" select="/f:Bundle/f:entry/f:resource/f:PractitionerRole[concat('PractitionerRole/', f:id/@value) = $reference][1]" />
    <xsl:variable name="rolePractitionerRef" select="$role/f:practitioner/f:reference/@value" />
    <xsl:variable name="rolePractitioner" select="/f:Bundle/f:entry/f:resource/f:Practitioner[concat('Practitioner/', f:id/@value) = $rolePractitionerRef][1]" />
    <xsl:variable name="directPractitioner" select="/f:Bundle/f:entry/f:resource/f:Practitioner[concat('Practitioner/', f:id/@value) = $reference][1]" />
    <xsl:variable name="directOrganization" select="/f:Bundle/f:entry/f:resource/f:Organization[concat('Organization/', f:id/@value) = $reference][1]" />
    <xsl:variable name="roleOrganizationRef" select="$role/f:organization/f:reference/@value" />
    <xsl:variable name="roleOrganization" select="/f:Bundle/f:entry/f:resource/f:Organization[concat('Organization/', f:id/@value) = $roleOrganizationRef][1]" />

    <xsl:choose>
      <xsl:when test="$performer/@p:provider"><xsl:choose><xsl:when test="$performer/f:reference/@p:href"><a class="fhir-reference-link" href="{$performer/f:reference/@p:href}"><xsl:value-of select="$performer/@p:provider"/></a></xsl:when><xsl:otherwise><xsl:value-of select="$performer/@p:provider"/></xsl:otherwise></xsl:choose></xsl:when>
      <!-- Source display remains a fallback when no practitioner is referenced. -->
      <xsl:when test="$performer/f:display/@value">
        <xsl:value-of select="$performer/f:display/@value" />
      </xsl:when>

      <!-- PractitionerRole may itself provide a human-readable practitioner display. -->
      <xsl:when test="$role/f:practitioner/f:display/@value">
        <xsl:value-of select="$role/f:practitioner/f:display/@value" />
      </xsl:when>

      <!-- Resolve PractitionerRole.practitioner to the included Practitioner resource. -->
      <xsl:when test="$rolePractitioner/f:name">
        <xsl:call-template name="render-human-name">
          <xsl:with-param name="name" select="$rolePractitioner/f:name[1]" />
        </xsl:call-template>
      </xsl:when>

      <!-- Direct Practitioner performer. -->
      <xsl:when test="$directPractitioner/f:name">
        <xsl:call-template name="render-human-name">
          <xsl:with-param name="name" select="$directPractitioner/f:name[1]" />
        </xsl:call-template>
      </xsl:when>

      <!-- Organization performers are valid in FHIR Observation.performer too. -->
      <xsl:when test="$directOrganization/f:name/@value">
        <xsl:value-of select="$directOrganization/f:name/@value" />
      </xsl:when>

      <!-- If a role points only to an organization, show that organization. -->
      <xsl:when test="$role/f:organization/f:display/@value">
        <xsl:value-of select="$role/f:organization/f:display/@value" />
      </xsl:when>
      <xsl:when test="$roleOrganization/f:name/@value">
        <xsl:value-of select="$roleOrganization/f:name/@value" />
      </xsl:when>

      <!-- Last resort: preserve the original FHIR reference for troubleshooting. -->
      <xsl:when test="$reference">
        <span style="font-size:0.82rem;opacity:0.8;"><xsl:value-of select="$reference" /></span>
      </xsl:when>
      <xsl:otherwise>—</xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template name="render-human-name">
    <xsl:param name="name" />
    <xsl:choose>
      <!-- FHIR HumanName.text is the preferred ready-to-display representation. -->
      <xsl:when test="$name/f:text/@value">
        <xsl:value-of select="$name/f:text/@value" />
      </xsl:when>
      <xsl:otherwise>
        <xsl:for-each select="$name/f:prefix/@value">
          <xsl:if test="position() &gt; 1"><xsl:text> </xsl:text></xsl:if>
          <xsl:value-of select="." />
        </xsl:for-each>
        <xsl:if test="$name/f:prefix/@value and $name/f:given/@value"><xsl:text> </xsl:text></xsl:if>
        <xsl:for-each select="$name/f:given/@value">
          <xsl:if test="position() &gt; 1"><xsl:text> </xsl:text></xsl:if>
          <xsl:value-of select="." />
        </xsl:for-each>
        <xsl:if test="$name/f:given/@value and $name/f:family/@value"><xsl:text> </xsl:text></xsl:if>
        <xsl:value-of select="$name/f:family/@value" />
        <xsl:if test="$name/f:suffix/@value"><xsl:text> </xsl:text></xsl:if>
        <xsl:for-each select="$name/f:suffix/@value">
          <xsl:if test="position() &gt; 1"><xsl:text> </xsl:text></xsl:if>
          <xsl:value-of select="." />
        </xsl:for-each>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>

  <xsl:template match="f:Observation" mode="result-row">
    <!--
      FHIR Observation.interpretation is intentionally handled generically.
      Common codes include H/HH/L/LL/A/AA/N, and some source systems prefix
      them with an asterisk (for example *H or *L).  We normalize only for
      styling while preserving the source interpretation for display.
    -->
    <xsl:variable name="interpretationRaw">
      <xsl:choose>
        <xsl:when test="f:interpretation/f:coding[1]/f:code/@value"><xsl:value-of select="f:interpretation/f:coding[1]/f:code/@value" /></xsl:when>
        <xsl:when test="f:interpretation/f:coding[1]/f:display/@value"><xsl:value-of select="f:interpretation/f:coding[1]/f:display/@value" /></xsl:when>
        <xsl:when test="f:interpretation/f:text/@value"><xsl:value-of select="f:interpretation/f:text/@value" /></xsl:when>
      </xsl:choose>
    </xsl:variable>
    <xsl:variable name="interpretation" select="translate(translate(normalize-space($interpretationRaw), $lower, $upper), '* ', '')" />
    <xsl:variable name="isHigh" select="$interpretation = 'H' or $interpretation = 'HH' or contains($interpretation, 'HIGH')" />
    <xsl:variable name="isLow" select="$interpretation = 'L' or $interpretation = 'LL' or contains($interpretation, 'LOW')" />
    <xsl:variable name="isCritical" select="$interpretation = 'HH' or $interpretation = 'LL' or $interpretation = 'AA' or contains($interpretation, 'CRIT')" />
    <xsl:variable name="isAbnormal" select="$isHigh or $isLow or $interpretation = 'A' or $interpretation = 'AA' or contains($interpretation, 'ABNORMAL') or $isCritical" />

    <tr>
      <xsl:if test="$isAbnormal">
        <xsl:attribute name="style">background:rgba(185, 28, 28, 0.055);</xsl:attribute>
      </xsl:if>
      <td>
        <div class="result-name">
          <xsl:choose>
            <xsl:when test="f:code/f:text/@value"><xsl:value-of select="f:code/f:text/@value" /></xsl:when>
            <xsl:when test="f:code/f:coding/f:display/@value"><xsl:value-of select="f:code/f:coding/f:display/@value" /></xsl:when>
            <xsl:otherwise><xsl:value-of select="f:code/f:coding/f:code/@value" /></xsl:otherwise>
          </xsl:choose>
        </div>
        <div class="result-code"><xsl:value-of select="f:code/f:coding[1]/f:code/@value" /></div>
      </td>
      <td>
        <span>
          <xsl:if test="$isAbnormal">
            <xsl:attribute name="style">font-weight:700;color:#b42318;</xsl:attribute>
          </xsl:if>
          <xsl:choose>
            <xsl:when test="f:valueQuantity"><xsl:value-of select="f:valueQuantity/f:value/@value" /><xsl:text> </xsl:text><xsl:value-of select="f:valueQuantity/f:unit/@value" /></xsl:when>
            <xsl:when test="f:valueString/@value"><xsl:value-of select="f:valueString/@value" /></xsl:when>
            <xsl:when test="f:valueCodeableConcept/f:text/@value"><xsl:value-of select="f:valueCodeableConcept/f:text/@value" /></xsl:when>
            <xsl:when test="f:valueCodeableConcept/f:coding/f:display/@value"><xsl:value-of select="f:valueCodeableConcept/f:coding/f:display/@value" /></xsl:when>
            <xsl:when test="f:valueInteger/@value"><xsl:value-of select="f:valueInteger/@value" /></xsl:when>
            <xsl:when test="f:valueBoolean/@value"><xsl:value-of select="f:valueBoolean/@value" /></xsl:when>
            <xsl:otherwise>—</xsl:otherwise>
          </xsl:choose>
        </span>
      </td>
      <td>
        <xsl:choose>
          <xsl:when test="normalize-space($interpretationRaw) != ''">
            <span>
              <xsl:attribute name="style">
                <xsl:text>display:inline-block;min-width:2.2em;text-align:center;padding:0.15rem 0.45rem;border-radius:999px;font-weight:700;font-size:0.8rem;</xsl:text>
                <xsl:choose>
                  <xsl:when test="$isCritical">background:#7f1d1d;color:#fff;</xsl:when>
                  <xsl:when test="$isAbnormal">background:#fee4e2;color:#b42318;border:1px solid #fecdca;</xsl:when>
                  <xsl:otherwise>background:#eef2f6;color:#475467;border:1px solid #d0d5dd;</xsl:otherwise>
                </xsl:choose>
              </xsl:attribute>
              <xsl:value-of select="$interpretationRaw" />
            </span>
          </xsl:when>
          <xsl:otherwise>—</xsl:otherwise>
        </xsl:choose>
      </td>
      <td>
        <xsl:choose>
          <!-- Many FHIR feeds populate only referenceRange.text. -->
          <xsl:when test="f:referenceRange[1]/f:text/@value">
            <xsl:value-of select="f:referenceRange[1]/f:text/@value" />
          </xsl:when>
          <xsl:when test="f:referenceRange[1]">
            <xsl:if test="f:referenceRange[1]/f:low/f:value/@value"><xsl:value-of select="f:referenceRange[1]/f:low/f:value/@value" /></xsl:if>
            <xsl:if test="f:referenceRange[1]/f:low/f:value/@value and f:referenceRange[1]/f:high/f:value/@value"><xsl:text> - </xsl:text></xsl:if>
            <xsl:if test="f:referenceRange[1]/f:high/f:value/@value"><xsl:value-of select="f:referenceRange[1]/f:high/f:value/@value" /></xsl:if>
            <xsl:text> </xsl:text><xsl:value-of select="f:referenceRange[1]/f:high/f:unit/@value | f:referenceRange[1]/f:low/f:unit/@value" />
          </xsl:when>
          <xsl:otherwise>—</xsl:otherwise>
        </xsl:choose>
      </td>
      <td>
        <xsl:choose>
          <xsl:when test="f:effectiveDateTime/@value">
            <xsl:value-of select="translate(substring(f:effectiveDateTime/@value, 1, 16), 'T', ' ')" />
          </xsl:when>
          <xsl:when test="f:effectivePeriod/f:start/@value">
            <xsl:value-of select="translate(substring(f:effectivePeriod/f:start/@value, 1, 16), 'T', ' ')" />
          </xsl:when>
          <xsl:otherwise>—</xsl:otherwise>
        </xsl:choose>
      </td>
      <td>
        <xsl:call-template name="render-performers" />
      </td>
      <td><xsl:value-of select="f:status/@value" /></td>
    </tr>

    <!-- Observation notes belong directly with the result rather than being dropped. -->
    <xsl:if test="f:note/f:text/@value">
      <tr>
        <td colspan="7" style="padding-top:0;border-top:0;">
          <div style="margin:0.15rem 0 0.65rem 0;padding:0.55rem 0.75rem;border-left:3px solid var(--primary-color);background:rgba(127,127,127,0.055);border-radius:0 0.35rem 0.35rem 0;">
            <div style="font-size:0.75rem;font-weight:700;text-transform:uppercase;letter-spacing:0.04em;opacity:0.7;margin-bottom:0.3rem;">Notes</div>
            <ul style="margin:0;padding-left:1.1rem;">
              <xsl:for-each select="f:note/f:text/@value">
                <li style="margin:0.18rem 0;line-height:1.4;white-space:pre-wrap;"><xsl:value-of select="." /></li>
              </xsl:for-each>
            </ul>
          </div>
        </td>
      </tr>
    </xsl:if>
  </xsl:template>
</xsl:stylesheet>
