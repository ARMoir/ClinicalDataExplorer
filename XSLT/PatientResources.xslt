<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:f="http://hl7.org/fhir" xmlns:p="urn:clinical-data-explorer:presentation" exclude-result-prefixes="f p">
  <xsl:output method="html" omit-xml-declaration="yes"/>
  <xsl:param name="facilityName"/><xsl:param name="logoPath"/><xsl:param name="primaryColor"/><xsl:param name="secondaryColor"/>
  <xsl:param name="resourceLabel">Record</xsl:param>
  <xsl:param name="expandDetails">false</xsl:param>
  <xsl:variable name="upper" select="'ABCDEFGHIJKLMNOPQRSTUVWXYZ'" />
  <xsl:variable name="lower" select="'abcdefghijklmnopqrstuvwxyz'" />
  <xsl:template match="/">
    <div class="resource-records" style="--primary-color:{$primaryColor};--secondary-color:{$secondaryColor}">
      <xsl:variable name="records" select="f:Bundle/f:entry/f:resource/*"/>
      <xsl:choose>
      <xsl:when test="$records and not($records[not(self::f:Observation)]) and not($records/descendant::*/@value[contains(., '&#10;') or contains(., '\n')])">
        <div class="table-wrap"><table class="results-table patient-lab-results">
          <thead><tr><th scope="col">Test</th><th scope="col">Result</th><th scope="col">Flag</th><th scope="col">Reference range</th><th scope="col">Date</th><th scope="col">Provider</th><th scope="col">Status</th></tr></thead>
          <xsl:for-each select="$records"><tbody class="lab-record">
            <xsl:if test="@p:anchor"><xsl:attribute name="id"><xsl:value-of select="@p:anchor"/></xsl:attribute></xsl:if>
            <xsl:apply-templates select="." mode="lab-row"/>
            <xsl:apply-templates select="f:component" mode="lab-row"/>
            <tr class="lab-details-row"><td colspan="7">
              <xsl:for-each select="f:note/f:text"><p class="lab-note"><strong>Note: </strong><xsl:value-of select="@value"/></p></xsl:for-each>
              <details class="resource-all-fields"><xsl:if test="$expandDetails = 'true'"><xsl:attribute name="open">open</xsl:attribute></xsl:if><summary>All record details</summary><dl class="resource-fields"><xsl:apply-templates select="*" mode="field"/></dl></details>
            </td></tr>
          </tbody></xsl:for-each>
        </table></div>
      </xsl:when>
      <xsl:otherwise>
      <xsl:for-each select="$records">
        <xsl:variable name="reportText" select="self::f:Observation/descendant::*/@value[contains(., '&#10;') or contains(., '\n')]" />
        <article class="resource-record">
          <xsl:if test="@p:anchor"><xsl:attribute name="id"><xsl:value-of select="@p:anchor"/></xsl:attribute></xsl:if>
          <header><h3><xsl:choose><xsl:when test="f:code/f:text/@value"><xsl:value-of select="f:code/f:text/@value"/></xsl:when><xsl:when test="f:code/f:coding/f:display/@value"><xsl:value-of select="f:code/f:coding[1]/f:display/@value"/></xsl:when><xsl:when test="f:type/f:text/@value"><xsl:value-of select="f:type[1]/f:text/@value"/></xsl:when><xsl:when test="f:name/f:text/@value"><xsl:value-of select="f:name[1]/f:text/@value"/></xsl:when><xsl:when test="f:name/@value"><xsl:value-of select="f:name/@value"/></xsl:when><xsl:when test="f:title/@value"><xsl:value-of select="f:title/@value"/></xsl:when><xsl:otherwise><xsl:value-of select="$resourceLabel"/></xsl:otherwise></xsl:choose></h3>
          <xsl:if test="self::f:Encounter"><a href="/encounter/{f:id/@value}">Open encounter report →</a></xsl:if></header>
          <div class="resource-summary"><span>Record ID: <xsl:value-of select="f:id/@value"/></span><xsl:if test="f:status/@value"><span class="status-chip"><xsl:value-of select="f:status/@value"/></span></xsl:if><span><xsl:value-of select="f:effectiveDateTime/@value | f:period/f:start/@value | f:authoredOn/@value | f:recordedDate/@value | f:date/@value"/></span></div>
          <xsl:if test="not($reportText) and (f:valueQuantity or f:valueString or f:valueCodeableConcept or f:valueBoolean or f:valueInteger)"><p class="observation-value"><xsl:value-of select="f:valueQuantity/f:comparator/@value"/><xsl:value-of select="f:valueQuantity/f:value/@value | f:valueString/@value | f:valueBoolean/@value | f:valueInteger/@value"/><xsl:text> </xsl:text><xsl:choose><xsl:when test="f:valueQuantity/f:unit/@value"><xsl:value-of select="f:valueQuantity/f:unit/@value"/></xsl:when><xsl:otherwise><xsl:value-of select="f:valueQuantity/f:code/@value"/></xsl:otherwise></xsl:choose><xsl:choose><xsl:when test="f:valueCodeableConcept/f:text/@value"><xsl:value-of select="f:valueCodeableConcept/f:text/@value"/></xsl:when><xsl:otherwise><xsl:value-of select="f:valueCodeableConcept/f:coding[1]/f:display/@value"/></xsl:otherwise></xsl:choose></p></xsl:if>
          <xsl:for-each select="$reportText"><div class="clinical-document"><xsl:call-template name="render-report-lines"><xsl:with-param name="text" select="."/></xsl:call-template></div></xsl:for-each>
          <dl class="record-highlights"><xsl:apply-templates select="." mode="highlights"/></dl>
          <details class="resource-all-fields"><xsl:if test="$expandDetails = 'true'"><xsl:attribute name="open">open</xsl:attribute></xsl:if><summary>All record details</summary><dl class="resource-fields"><xsl:apply-templates select="*" mode="field"/></dl></details>
        </article>
      </xsl:for-each>
      </xsl:otherwise></xsl:choose>
    </div>
  </xsl:template>
  <xsl:template match="f:Observation | f:component" mode="lab-row">
    <xsl:variable name="observation" select="ancestor-or-self::f:Observation[1]"/>
    <xsl:variable name="flag"><xsl:choose><xsl:when test="f:interpretation/f:coding/f:code/@value"><xsl:value-of select="f:interpretation[1]/f:coding[1]/f:code/@value"/></xsl:when><xsl:otherwise><xsl:apply-templates select="f:interpretation[1]" mode="readable"/></xsl:otherwise></xsl:choose></xsl:variable>
    <xsl:variable name="normalizedFlag" select="translate(translate(normalize-space($flag), $lower, $upper), '* ', '')"/>
    <xsl:variable name="critical" select="$normalizedFlag='HH' or $normalizedFlag='LL' or $normalizedFlag='AA' or contains($normalizedFlag, 'CRIT')"/>
    <xsl:variable name="abnormal" select="$critical or $normalizedFlag='H' or $normalizedFlag='L' or $normalizedFlag='A' or contains($normalizedFlag, 'HIGH') or contains($normalizedFlag, 'LOW') or contains($normalizedFlag, 'ABNORMAL')"/>
    <tr>
      <xsl:attribute name="class"><xsl:text>lab-result-row</xsl:text><xsl:if test="self::f:component"><xsl:text> lab-component</xsl:text></xsl:if><xsl:if test="$abnormal"><xsl:text> lab-abnormal</xsl:text></xsl:if></xsl:attribute>
      <td><strong class="result-name"><xsl:choose><xsl:when test="f:code"><xsl:apply-templates select="f:code" mode="readable"/></xsl:when><xsl:otherwise>Observation</xsl:otherwise></xsl:choose></strong><xsl:if test="f:code/f:coding/f:code/@value"><div class="result-code"><xsl:value-of select="f:code/f:coding[1]/f:code/@value"/></div></xsl:if><xsl:if test="self::f:Observation"><div class="result-code">Record ID: <xsl:value-of select="f:id/@value"/></div></xsl:if></td>
      <td class="lab-value"><xsl:choose>
        <xsl:when test="*[starts-with(local-name(), 'value')]"><xsl:apply-templates select="*[starts-with(local-name(), 'value')][1]" mode="readable"/></xsl:when>
        <xsl:when test="f:dataAbsentReason"><xsl:apply-templates select="f:dataAbsentReason" mode="readable"/></xsl:when>
        <xsl:when test="f:component">See component results</xsl:when>
        <xsl:otherwise>—</xsl:otherwise>
      </xsl:choose></td>
      <td><xsl:choose><xsl:when test="string-length($flag) &gt; 0"><span><xsl:attribute name="class"><xsl:text>lab-flag</xsl:text><xsl:if test="$critical"><xsl:text> lab-critical</xsl:text></xsl:if></xsl:attribute><xsl:value-of select="$flag"/></span></xsl:when><xsl:otherwise>—</xsl:otherwise></xsl:choose></td>
      <td><xsl:choose><xsl:when test="f:referenceRange"><xsl:for-each select="f:referenceRange"><xsl:if test="position() &gt; 1"><br/></xsl:if><xsl:apply-templates select="." mode="readable"/></xsl:for-each></xsl:when><xsl:otherwise>—</xsl:otherwise></xsl:choose></td>
      <td class="lab-date"><xsl:choose>
        <xsl:when test="$observation/f:effectiveDateTime/@value"><xsl:value-of select="$observation/f:effectiveDateTime/@value"/></xsl:when>
        <xsl:when test="$observation/f:effectiveInstant/@value"><xsl:value-of select="$observation/f:effectiveInstant/@value"/></xsl:when>
        <xsl:when test="$observation/f:effectivePeriod"><xsl:apply-templates select="$observation/f:effectivePeriod" mode="readable"/></xsl:when>
        <xsl:when test="$observation/f:issued/@value"><xsl:value-of select="$observation/f:issued/@value"/></xsl:when>
        <xsl:when test="$observation/f:meta/f:lastUpdated/@value"><span class="result-code">Updated: </span><xsl:value-of select="$observation/f:meta/f:lastUpdated/@value"/></xsl:when>
        <xsl:otherwise>—</xsl:otherwise>
      </xsl:choose></td>
      <td><xsl:choose><xsl:when test="$observation/f:performer"><xsl:for-each select="$observation/f:performer"><xsl:if test="position() &gt; 1"><br/></xsl:if><xsl:apply-templates select="." mode="readable"/></xsl:for-each></xsl:when><xsl:otherwise>—</xsl:otherwise></xsl:choose></td>
      <td><span class="status-chip"><xsl:value-of select="$observation/f:status/@value"/></span></td>
    </tr>
  </xsl:template>
  <xsl:template match="*" mode="highlights"><xsl:apply-templates select="f:identifier | f:description | f:type | f:category | f:period | f:author | f:performer | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Patient" mode="highlights"><xsl:apply-templates select="f:name | f:birthDate | f:gender | f:telecom | f:address | f:maritalStatus | f:generalPractitioner | f:managingOrganization | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Account" mode="highlights"><xsl:apply-templates select="f:name | f:type | f:servicePeriod | f:coverage | f:guarantor | f:owner | f:description | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Appointment | f:AppointmentResponse" mode="highlights"><xsl:apply-templates select="f:description | f:start | f:end | f:serviceType | f:specialty | f:appointmentType | f:reasonCode | f:reasonReference | f:participant | f:actor | f:participantStatus | f:comment | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Encounter | f:EpisodeOfCare" mode="highlights"><xsl:apply-templates select="f:class | f:type | f:period | f:reasonCode | f:reasonReference | f:diagnosis | f:participant | f:serviceProvider | f:location | f:hospitalization | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:PractitionerRole" mode="highlights"><xsl:apply-templates select="f:practitioner | f:organization | f:code | f:specialty | f:location | f:telecom | f:availableTime | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Practitioner | f:RelatedPerson | f:Person" mode="highlights"><xsl:apply-templates select="f:name | f:relationship | f:telecom | f:address | f:birthDate | f:gender | f:qualification | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:CareTeam" mode="highlights"><xsl:apply-templates select="f:name | f:category | f:period | f:participant | f:managingOrganization | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Coverage" mode="highlights"><xsl:apply-templates select="f:type | f:subscriber | f:subscriberId | f:beneficiary | f:relationship | f:payor | f:period | f:class | f:costToBeneficiary | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:ServiceRequest" mode="highlights"><xsl:apply-templates select="f:intent | f:priority | f:code | f:category | f:occurrenceDateTime | f:occurrencePeriod | f:requester | f:performer | f:reasonCode | f:reasonReference | f:bodySite | f:specimen | f:patientInstruction | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:DiagnosticReport" mode="highlights"><xsl:apply-templates select="f:category | f:effectivePeriod | f:issued | f:performer | f:resultsInterpreter | f:conclusion | f:conclusionCode | f:result | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Observation" mode="highlights"><xsl:apply-templates select="f:category | f:interpretation | f:referenceRange | f:method | f:bodySite | f:performer | f:component | f:dataAbsentReason | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:ImagingStudy" mode="highlights"><xsl:apply-templates select="f:started | f:modality | f:description | f:numberOfSeries | f:numberOfInstances | f:procedureCode | f:reasonCode | f:referrer | f:interpreter | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Specimen" mode="highlights"><xsl:apply-templates select="f:type | f:collection | f:receivedTime | f:condition | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Condition" mode="highlights"><xsl:apply-templates select="f:clinicalStatus | f:verificationStatus | f:severity | f:category | f:bodySite | f:onsetDateTime | f:onsetAge | f:onsetPeriod | f:abatementDateTime | f:recordedDate | f:recorder | f:asserter | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:AllergyIntolerance" mode="highlights"><xsl:apply-templates select="f:clinicalStatus | f:verificationStatus | f:type | f:category | f:criticality | f:reaction | f:onsetDateTime | f:lastOccurrence | f:recorder | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Flag | f:DetectedIssue | f:AdverseEvent | f:RiskAssessment" mode="highlights"><xsl:apply-templates select="f:category | f:code | f:criticality | f:severity | f:detail | f:mitigation | f:event | f:actuality | f:date | f:outcome | f:probabilityDecimal | f:prediction | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:MedicationRequest | f:MedicationStatement | f:MedicationAdministration | f:MedicationDispense" mode="highlights"><xsl:apply-templates select="f:medicationCodeableConcept | f:medicationReference | f:intent | f:priority | f:effectiveDateTime | f:effectivePeriod | f:whenHandedOver | f:dosageInstruction | f:dosage | f:reasonCode | f:reasonReference | f:requester | f:performer | f:prescriber | f:informationSource | f:quantity | f:daysSupply | f:dispenseRequest | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Procedure" mode="highlights"><xsl:apply-templates select="f:performedDateTime | f:performedPeriod | f:reasonCode | f:bodySite | f:performer | f:outcome | f:complication | f:followUp | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Immunization | f:ImmunizationEvaluation | f:ImmunizationRecommendation" mode="highlights"><xsl:apply-templates select="f:vaccineCode | f:occurrenceDateTime | f:primarySource | f:lotNumber | f:expirationDate | f:site | f:route | f:doseQuantity | f:performer | f:protocolApplied | f:recommendation | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:ClinicalImpression | f:FamilyMemberHistory" mode="highlights"><xsl:apply-templates select="f:description | f:summary | f:date | f:relationship | f:sex | f:bornDate | f:age | f:condition | f:finding | f:assessor | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:CarePlan | f:Goal" mode="highlights"><xsl:apply-templates select="f:description | f:category | f:intent | f:priority | f:period | f:startDate | f:target | f:addresses | f:activity | f:achievementStatus | f:lifecycleStatus | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:NutritionOrder" mode="highlights"><xsl:apply-templates select="f:dateTime | f:orderer | f:oralDiet | f:supplement | f:enteralFormula | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Device | f:DeviceRequest | f:DeviceUseStatement" mode="highlights"><xsl:apply-templates select="f:codeCodeableConcept | f:codeReference | f:type | f:deviceName | f:manufacturer | f:modelNumber | f:timingPeriod | f:timingDateTime | f:bodySite | f:reasonCode | f:device | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Communication | f:CommunicationRequest | f:Task | f:RequestGroup" mode="highlights"><xsl:apply-templates select="f:priority | f:intent | f:code | f:description | f:authoredOn | f:sent | f:received | f:executionPeriod | f:requester | f:sender | f:recipient | f:owner | f:payload | f:reasonCode | f:action | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:QuestionnaireResponse" mode="highlights"><xsl:apply-templates select="f:questionnaire | f:authored | f:author | f:source | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Composition | f:DocumentReference | f:DocumentManifest | f:Media" mode="highlights"><xsl:apply-templates select="f:type | f:category | f:date | f:author | f:custodian | f:description | f:title | f:docStatus | f:context | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Consent" mode="highlights"><xsl:apply-templates select="f:scope | f:category | f:dateTime | f:performer | f:organization | f:provision | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Organization | f:OrganizationAffiliation | f:Location | f:HealthcareService" mode="highlights"><xsl:apply-templates select="f:name | f:type | f:description | f:specialty | f:address | f:telecom | f:partOf | f:managingOrganization | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:Claim | f:ClaimResponse | f:ExplanationOfBenefit | f:Invoice | f:ChargeItem" mode="highlights"><xsl:apply-templates select="f:type | f:use | f:priority | f:billablePeriod | f:created | f:provider | f:insurer | f:outcome | f:disposition | f:total | f:amount | f:quantity | f:unitPrice | f:billingCode | f:payment | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:SupplyRequest | f:SupplyDelivery" mode="highlights"><xsl:apply-templates select="f:itemCodeableConcept | f:itemReference | f:quantity | f:occurrenceDateTime | f:occurrencePeriod | f:supplier | f:destination | f:receiver | f:identifier | f:note" mode="highlight"/></xsl:template>
  <xsl:template match="f:name" mode="highlight-label">Name</xsl:template>
  <xsl:template match="f:birthDate" mode="highlight-label">Date of birth</xsl:template>
  <xsl:template match="f:gender" mode="highlight-label">Gender</xsl:template>
  <xsl:template match="f:telecom" mode="highlight-label">Contact</xsl:template>
  <xsl:template match="f:address" mode="highlight-label">Address</xsl:template>
  <xsl:template match="f:maritalStatus" mode="highlight-label">Marital status</xsl:template>
  <xsl:template match="f:generalPractitioner" mode="highlight-label">Primary clinician</xsl:template>
  <xsl:template match="f:managingOrganization" mode="highlight-label">Managing organization</xsl:template>
  <xsl:template match="f:identifier" mode="highlight-label">Identifiers</xsl:template>
  <xsl:template match="f:type" mode="highlight-label">Type</xsl:template>
  <xsl:template match="f:servicePeriod" mode="highlight-label">Service period</xsl:template>
  <xsl:template match="f:coverage" mode="highlight-label">Coverage</xsl:template>
  <xsl:template match="f:guarantor" mode="highlight-label">Guarantor</xsl:template>
  <xsl:template match="f:owner" mode="highlight-label">Responsible party</xsl:template>
  <xsl:template match="f:description" mode="highlight-label">Description</xsl:template>
  <xsl:template match="f:start" mode="highlight-label">Start</xsl:template>
  <xsl:template match="f:end" mode="highlight-label">End</xsl:template>
  <xsl:template match="f:serviceType" mode="highlight-label">Service</xsl:template>
  <xsl:template match="f:specialty" mode="highlight-label">Specialty</xsl:template>
  <xsl:template match="f:appointmentType" mode="highlight-label">Appointment type</xsl:template>
  <xsl:template match="f:reasonCode" mode="highlight-label">Reason</xsl:template>
  <xsl:template match="f:reasonReference" mode="highlight-label">Reason</xsl:template>
  <xsl:template match="f:participant" mode="highlight-label">Participants</xsl:template>
  <xsl:template match="f:actor" mode="highlight-label">Participant</xsl:template>
  <xsl:template match="f:participantStatus" mode="highlight-label">Response</xsl:template>
  <xsl:template match="f:comment" mode="highlight-label">Comment</xsl:template>
  <xsl:template match="f:class" mode="highlight-label">Class</xsl:template>
  <xsl:template match="f:period" mode="highlight-label">Period</xsl:template>
  <xsl:template match="f:diagnosis" mode="highlight-label">Diagnoses</xsl:template>
  <xsl:template match="f:serviceProvider" mode="highlight-label">Care organization</xsl:template>
  <xsl:template match="f:location" mode="highlight-label">Location</xsl:template>
  <xsl:template match="f:hospitalization" mode="highlight-label">Hospital stay</xsl:template>
  <xsl:template match="f:practitioner" mode="highlight-label">Provider</xsl:template>
  <xsl:template match="f:organization" mode="highlight-label">Organization</xsl:template>
  <xsl:template match="f:code" mode="highlight-label">Role / code</xsl:template>
  <xsl:template match="f:availableTime" mode="highlight-label">Availability</xsl:template>
  <xsl:template match="f:relationship" mode="highlight-label">Relationship</xsl:template>
  <xsl:template match="f:qualification" mode="highlight-label">Qualifications</xsl:template>
  <xsl:template match="f:category" mode="highlight-label">Category</xsl:template>
  <xsl:template match="f:subscriber" mode="highlight-label">Subscriber</xsl:template>
  <xsl:template match="f:subscriberId" mode="highlight-label">Subscriber ID</xsl:template>
  <xsl:template match="f:beneficiary" mode="highlight-label">Beneficiary</xsl:template>
  <xsl:template match="f:payor" mode="highlight-label">Payer</xsl:template>
  <xsl:template match="f:costToBeneficiary" mode="highlight-label">Patient cost</xsl:template>
  <xsl:template match="f:intent" mode="highlight-label">Intent</xsl:template>
  <xsl:template match="f:priority" mode="highlight-label">Priority</xsl:template>
  <xsl:template match="f:occurrenceDateTime" mode="highlight-label">Scheduled / occurred</xsl:template>
  <xsl:template match="f:occurrencePeriod" mode="highlight-label">Scheduled period</xsl:template>
  <xsl:template match="f:requester" mode="highlight-label">Requested by</xsl:template>
  <xsl:template match="f:performer" mode="highlight-label">Performed by</xsl:template>
  <xsl:template match="f:bodySite" mode="highlight-label">Body site</xsl:template>
  <xsl:template match="f:specimen" mode="highlight-label">Specimen</xsl:template>
  <xsl:template match="f:patientInstruction" mode="highlight-label">Patient instructions</xsl:template>
  <xsl:template match="f:effectivePeriod" mode="highlight-label">Effective period</xsl:template>
  <xsl:template match="f:issued" mode="highlight-label">Issued</xsl:template>
  <xsl:template match="f:resultsInterpreter" mode="highlight-label">Interpreted by</xsl:template>
  <xsl:template match="f:conclusion" mode="highlight-label">Conclusion</xsl:template>
  <xsl:template match="f:conclusionCode" mode="highlight-label">Conclusion</xsl:template>
  <xsl:template match="f:result" mode="highlight-label">Results</xsl:template>
  <xsl:template match="f:interpretation" mode="highlight-label">Interpretation</xsl:template>
  <xsl:template match="f:referenceRange" mode="highlight-label">Reference range</xsl:template>
  <xsl:template match="f:method" mode="highlight-label">Method</xsl:template>
  <xsl:template match="f:component" mode="highlight-label">Component result</xsl:template>
  <xsl:template match="f:dataAbsentReason" mode="highlight-label">Reason result unavailable</xsl:template>
  <xsl:template match="f:started" mode="highlight-label">Started</xsl:template>
  <xsl:template match="f:modality" mode="highlight-label">Modality</xsl:template>
  <xsl:template match="f:numberOfSeries" mode="highlight-label">Series</xsl:template>
  <xsl:template match="f:numberOfInstances" mode="highlight-label">Images</xsl:template>
  <xsl:template match="f:procedureCode" mode="highlight-label">Procedure</xsl:template>
  <xsl:template match="f:referrer" mode="highlight-label">Referred by</xsl:template>
  <xsl:template match="f:interpreter" mode="highlight-label">Interpreted by</xsl:template>
  <xsl:template match="f:collection" mode="highlight-label">Collection</xsl:template>
  <xsl:template match="f:receivedTime" mode="highlight-label">Received</xsl:template>
  <xsl:template match="f:condition" mode="highlight-label">Condition</xsl:template>
  <xsl:template match="f:clinicalStatus" mode="highlight-label">Clinical status</xsl:template>
  <xsl:template match="f:verificationStatus" mode="highlight-label">Verification</xsl:template>
  <xsl:template match="f:severity" mode="highlight-label">Severity</xsl:template>
  <xsl:template match="f:onsetDateTime" mode="highlight-label">Onset</xsl:template>
  <xsl:template match="f:onsetAge" mode="highlight-label">Age at onset</xsl:template>
  <xsl:template match="f:onsetPeriod" mode="highlight-label">Onset period</xsl:template>
  <xsl:template match="f:abatementDateTime" mode="highlight-label">Resolved</xsl:template>
  <xsl:template match="f:recordedDate" mode="highlight-label">Recorded</xsl:template>
  <xsl:template match="f:recorder" mode="highlight-label">Recorded by</xsl:template>
  <xsl:template match="f:asserter" mode="highlight-label">Reported by</xsl:template>
  <xsl:template match="f:criticality" mode="highlight-label">Criticality</xsl:template>
  <xsl:template match="f:reaction" mode="highlight-label">Reaction</xsl:template>
  <xsl:template match="f:lastOccurrence" mode="highlight-label">Last occurrence</xsl:template>
  <xsl:template match="f:detail" mode="highlight-label">Details</xsl:template>
  <xsl:template match="f:mitigation" mode="highlight-label">Mitigation</xsl:template>
  <xsl:template match="f:event" mode="highlight-label">Event</xsl:template>
  <xsl:template match="f:actuality" mode="highlight-label">Actuality</xsl:template>
  <xsl:template match="f:date" mode="highlight-label">Date</xsl:template>
  <xsl:template match="f:outcome" mode="highlight-label">Outcome</xsl:template>
  <xsl:template match="f:prediction" mode="highlight-label">Prediction</xsl:template>
  <xsl:template match="f:probabilityDecimal" mode="highlight-label">Probability</xsl:template>
  <xsl:template match="f:medicationCodeableConcept" mode="highlight-label">Medication</xsl:template>
  <xsl:template match="f:medicationReference" mode="highlight-label">Medication</xsl:template>
  <xsl:template match="f:effectiveDateTime" mode="highlight-label">Effective date</xsl:template>
  <xsl:template match="f:whenHandedOver" mode="highlight-label">Handed over</xsl:template>
  <xsl:template match="f:dosageInstruction" mode="highlight-label">Instructions</xsl:template>
  <xsl:template match="f:dosage" mode="highlight-label">Dose and administration</xsl:template>
  <xsl:template match="f:prescriber" mode="highlight-label">Prescriber</xsl:template>
  <xsl:template match="f:informationSource" mode="highlight-label">Reported by</xsl:template>
  <xsl:template match="f:quantity" mode="highlight-label">Quantity</xsl:template>
  <xsl:template match="f:daysSupply" mode="highlight-label">Days supply</xsl:template>
  <xsl:template match="f:dispenseRequest" mode="highlight-label">Dispensing</xsl:template>
  <xsl:template match="f:performedDateTime" mode="highlight-label">Performed</xsl:template>
  <xsl:template match="f:performedPeriod" mode="highlight-label">Performed period</xsl:template>
  <xsl:template match="f:complication" mode="highlight-label">Complications</xsl:template>
  <xsl:template match="f:followUp" mode="highlight-label">Follow-up</xsl:template>
  <xsl:template match="f:vaccineCode" mode="highlight-label">Vaccine</xsl:template>
  <xsl:template match="f:primarySource" mode="highlight-label">Primary source</xsl:template>
  <xsl:template match="f:lotNumber" mode="highlight-label">Lot number</xsl:template>
  <xsl:template match="f:expirationDate" mode="highlight-label">Expiration</xsl:template>
  <xsl:template match="f:site" mode="highlight-label">Site</xsl:template>
  <xsl:template match="f:route" mode="highlight-label">Route</xsl:template>
  <xsl:template match="f:doseQuantity" mode="highlight-label">Dose</xsl:template>
  <xsl:template match="f:protocolApplied" mode="highlight-label">Protocol</xsl:template>
  <xsl:template match="f:recommendation" mode="highlight-label">Recommendation</xsl:template>
  <xsl:template match="f:summary" mode="highlight-label">Summary</xsl:template>
  <xsl:template match="f:sex" mode="highlight-label">Sex</xsl:template>
  <xsl:template match="f:bornDate" mode="highlight-label">Date of birth</xsl:template>
  <xsl:template match="f:age" mode="highlight-label">Age</xsl:template>
  <xsl:template match="f:finding" mode="highlight-label">Findings</xsl:template>
  <xsl:template match="f:assessor" mode="highlight-label">Assessed by</xsl:template>
  <xsl:template match="f:startDate" mode="highlight-label">Start date</xsl:template>
  <xsl:template match="f:target" mode="highlight-label">Target</xsl:template>
  <xsl:template match="f:addresses" mode="highlight-label">Addresses</xsl:template>
  <xsl:template match="f:activity" mode="highlight-label">Planned activities</xsl:template>
  <xsl:template match="f:achievementStatus" mode="highlight-label">Progress</xsl:template>
  <xsl:template match="f:lifecycleStatus" mode="highlight-label">Status</xsl:template>
  <xsl:template match="f:dateTime" mode="highlight-label">Date</xsl:template>
  <xsl:template match="f:orderer" mode="highlight-label">Ordered by</xsl:template>
  <xsl:template match="f:oralDiet" mode="highlight-label">Oral diet</xsl:template>
  <xsl:template match="f:supplement" mode="highlight-label">Supplement</xsl:template>
  <xsl:template match="f:enteralFormula" mode="highlight-label">Enteral formula</xsl:template>
  <xsl:template match="f:codeCodeableConcept" mode="highlight-label">Device</xsl:template>
  <xsl:template match="f:codeReference" mode="highlight-label">Device</xsl:template>
  <xsl:template match="f:deviceName" mode="highlight-label">Device name</xsl:template>
  <xsl:template match="f:manufacturer" mode="highlight-label">Manufacturer</xsl:template>
  <xsl:template match="f:modelNumber" mode="highlight-label">Model</xsl:template>
  <xsl:template match="f:timingPeriod" mode="highlight-label">Period of use</xsl:template>
  <xsl:template match="f:timingDateTime" mode="highlight-label">Time of use</xsl:template>
  <xsl:template match="f:device" mode="highlight-label">Device</xsl:template>
  <xsl:template match="f:authoredOn" mode="highlight-label">Authored</xsl:template>
  <xsl:template match="f:sent" mode="highlight-label">Sent</xsl:template>
  <xsl:template match="f:received" mode="highlight-label">Received</xsl:template>
  <xsl:template match="f:executionPeriod" mode="highlight-label">Execution period</xsl:template>
  <xsl:template match="f:sender" mode="highlight-label">Sender</xsl:template>
  <xsl:template match="f:recipient" mode="highlight-label">Recipient</xsl:template>
  <xsl:template match="f:payload" mode="highlight-label">Message</xsl:template>
  <xsl:template match="f:action" mode="highlight-label">Action</xsl:template>
  <xsl:template match="f:questionnaire" mode="highlight-label">Questionnaire</xsl:template>
  <xsl:template match="f:authored" mode="highlight-label">Completed</xsl:template>
  <xsl:template match="f:author" mode="highlight-label">Author</xsl:template>
  <xsl:template match="f:source" mode="highlight-label">Source</xsl:template>
  <xsl:template match="f:custodian" mode="highlight-label">Custodian</xsl:template>
  <xsl:template match="f:title" mode="highlight-label">Title</xsl:template>
  <xsl:template match="f:docStatus" mode="highlight-label">Document status</xsl:template>
  <xsl:template match="f:context" mode="highlight-label">Context</xsl:template>
  <xsl:template match="f:scope" mode="highlight-label">Scope</xsl:template>
  <xsl:template match="f:provision" mode="highlight-label">Consent provisions</xsl:template>
  <xsl:template match="f:partOf" mode="highlight-label">Part of</xsl:template>
  <xsl:template match="f:use" mode="highlight-label">Use</xsl:template>
  <xsl:template match="f:billablePeriod" mode="highlight-label">Billing period</xsl:template>
  <xsl:template match="f:created" mode="highlight-label">Created</xsl:template>
  <xsl:template match="f:provider" mode="highlight-label">Provider</xsl:template>
  <xsl:template match="f:insurer" mode="highlight-label">Insurer</xsl:template>
  <xsl:template match="f:disposition" mode="highlight-label">Disposition</xsl:template>
  <xsl:template match="f:total" mode="highlight-label">Total</xsl:template>
  <xsl:template match="f:amount" mode="highlight-label">Amount</xsl:template>
  <xsl:template match="f:unitPrice" mode="highlight-label">Unit price</xsl:template>
  <xsl:template match="f:billingCode" mode="highlight-label">Billing code</xsl:template>
  <xsl:template match="f:payment" mode="highlight-label">Payment</xsl:template>
  <xsl:template match="f:itemCodeableConcept" mode="highlight-label">Item</xsl:template>
  <xsl:template match="f:itemReference" mode="highlight-label">Item</xsl:template>
  <xsl:template match="f:supplier" mode="highlight-label">Supplier</xsl:template>
  <xsl:template match="f:destination" mode="highlight-label">Destination</xsl:template>
  <xsl:template match="f:receiver" mode="highlight-label">Receiver</xsl:template>
  <xsl:template match="f:note" mode="highlight-label">Notes</xsl:template>
  <xsl:template match="f:low" mode="highlight-label">Low</xsl:template>
  <xsl:template match="f:high" mode="highlight-label">High</xsl:template>
  <xsl:template match="f:valueQuantity" mode="highlight-label">Result</xsl:template>
  <xsl:template match="f:valueString" mode="highlight-label">Result</xsl:template>
  <xsl:template match="f:valueCodeableConcept" mode="highlight-label">Result</xsl:template>
  <xsl:template match="f:doseAndRate" mode="highlight-label">Dose and rate</xsl:template>
  <xsl:template match="f:timing" mode="highlight-label">Schedule</xsl:template>
  <xsl:template match="f:frequency" mode="highlight-label">Frequency</xsl:template>
  <xsl:template match="f:duration" mode="highlight-label">Duration</xsl:template>
  <xsl:template match="f:durationUnit" mode="highlight-label">Duration unit</xsl:template>
  <xsl:template match="f:periodUnit" mode="highlight-label">Period unit</xsl:template>
  <!-- Show selected clinical fields without requiring the raw field tree. -->
  <xsl:template match="*" mode="highlight">
    <xsl:if test="descendant-or-self::*/@value[string-length(.) &gt; 0]">
      <div class="record-highlight"><dt><strong><xsl:apply-templates select="." mode="highlight-label"/><xsl:text>:</xsl:text></strong></dt><dd><xsl:apply-templates select="." mode="readable"/></dd></div>
    </xsl:if>
  </xsl:template>
  <xsl:template match="*" mode="highlight-label"><xsl:value-of select="local-name()"/></xsl:template>
  <xsl:template match="*" mode="readable">
    <xsl:choose>
      <xsl:when test="@p:provider"><xsl:value-of select="@p:provider"/></xsl:when>
      <xsl:when test="@value"><xsl:value-of select="@value"/></xsl:when>
      <xsl:when test="f:text/@value"><xsl:value-of select="f:text/@value"/></xsl:when>
      <xsl:when test="f:display/@value"><xsl:value-of select="f:display/@value"/></xsl:when>
      <xsl:when test="f:reference/@value"><xsl:value-of select="f:reference/@value"/></xsl:when>
      <xsl:when test="f:coding"><xsl:apply-templates select="f:coding[1]" mode="readable"/></xsl:when>
      <xsl:when test="f:given or f:family"><xsl:for-each select="f:prefix | f:given | f:family | f:suffix"><xsl:if test="position() &gt; 1"><xsl:text> </xsl:text></xsl:if><xsl:value-of select="@value"/></xsl:for-each></xsl:when>
      <xsl:when test="f:start or f:end"><xsl:choose><xsl:when test="f:start/@value"><xsl:value-of select="f:start/@value"/></xsl:when><xsl:otherwise>Start not recorded</xsl:otherwise></xsl:choose><xsl:if test="f:end/@value"><xsl:text> – </xsl:text><xsl:value-of select="f:end/@value"/></xsl:if></xsl:when>
      <xsl:when test="f:value/@value"><xsl:if test="f:system/@value and not(f:unit or f:currency or f:code)"><xsl:value-of select="f:system/@value"/><xsl:text>: </xsl:text></xsl:if><xsl:value-of select="f:comparator/@value"/><xsl:value-of select="f:value/@value"/><xsl:text> </xsl:text><xsl:choose><xsl:when test="f:unit/@value"><xsl:value-of select="f:unit/@value"/></xsl:when><xsl:otherwise><xsl:value-of select="f:currency/@value | f:code/@value"/></xsl:otherwise></xsl:choose></xsl:when>
      <xsl:otherwise><xsl:for-each select="*[not(self::f:extension or self::f:id or self::f:system)]"><xsl:if test="position() &gt; 1"><xsl:text> · </xsl:text></xsl:if><xsl:if test="not(@value) and not(f:display or f:text or f:reference or f:coding)"><xsl:apply-templates select="." mode="highlight-label"/><xsl:text>: </xsl:text></xsl:if><xsl:apply-templates select="." mode="readable"/></xsl:for-each></xsl:otherwise>
    </xsl:choose>
  </xsl:template>
  <!-- Generic recursive fallback preserves every field, choice type, extension and contained resource.
       Add resource-specific templates in this stylesheet to customize clinical presentation.
       Narrative is rendered as text; server-provided HTML and URLs are never executed. -->
  <xsl:template match="*" mode="field">
    <div class="resource-field"><xsl:if test="@p:anchor"><xsl:attribute name="id"><xsl:value-of select="@p:anchor"/></xsl:attribute></xsl:if><dt><xsl:value-of select="local-name()"/></dt><dd>
      <xsl:if test="@p:provider"><span class="field-value"><strong>Provider: </strong><xsl:value-of select="@p:provider"/></span></xsl:if>
      <xsl:for-each select="@*[namespace-uri() != 'urn:clinical-data-explorer:presentation']"><span class="field-value"><xsl:if test="local-name() != 'value'"><strong><xsl:value-of select="local-name()"/>: </strong></xsl:if><xsl:choose><xsl:when test="local-name() = 'value' and parent::f:reference/@p:href"><a class="fhir-reference-link" href="{../@p:href}"><xsl:value-of select="."/></a></xsl:when><xsl:otherwise><xsl:value-of select="."/></xsl:otherwise></xsl:choose></span></xsl:for-each>
      <xsl:choose><xsl:when test="namespace-uri()='http://www.w3.org/1999/xhtml'"><span><xsl:value-of select="."/></span></xsl:when><xsl:when test="*"><dl><xsl:apply-templates select="*" mode="field"/></dl></xsl:when><xsl:otherwise><xsl:value-of select="text()"/></xsl:otherwise></xsl:choose>
    </dd></div>
  </xsl:template>
<!-- Report line formatting mirrors DiagnosticReportBundle.xslt. -->
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
</xsl:stylesheet>
