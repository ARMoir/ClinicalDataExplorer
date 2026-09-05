<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:f="http://hl7.org/fhir" exclude-result-prefixes="f">
  <xsl:output method="html" omit-xml-declaration="yes"/>
  <xsl:param name="facilityName"/><xsl:param name="logoPath"/><xsl:param name="primaryColor"/><xsl:param name="secondaryColor"/>
  <xsl:param name="resourceLabel">Record</xsl:param>
  <xsl:variable name="upper" select="'ABCDEFGHIJKLMNOPQRSTUVWXYZ'" />
  <xsl:variable name="lower" select="'abcdefghijklmnopqrstuvwxyz'" />
  <xsl:template match="/">
    <div class="resource-records" style="--primary-color:{$primaryColor};--secondary-color:{$secondaryColor}">
      <xsl:for-each select="f:Bundle/f:entry/f:resource/*">
        <xsl:variable name="reportText" select="self::f:Observation/descendant::*/@value[contains(., '&#10;') or contains(., '\n')]" />
        <article class="resource-record">
          <header><h3><xsl:choose><xsl:when test="f:code/f:text/@value"><xsl:value-of select="f:code/f:text/@value"/></xsl:when><xsl:when test="f:code/f:coding/f:display/@value"><xsl:value-of select="f:code/f:coding[1]/f:display/@value"/></xsl:when><xsl:when test="f:type/f:text/@value"><xsl:value-of select="f:type[1]/f:text/@value"/></xsl:when><xsl:when test="f:name/f:text/@value"><xsl:value-of select="f:name[1]/f:text/@value"/></xsl:when><xsl:when test="f:name/@value"><xsl:value-of select="f:name/@value"/></xsl:when><xsl:when test="f:title/@value"><xsl:value-of select="f:title/@value"/></xsl:when><xsl:otherwise><xsl:value-of select="$resourceLabel"/></xsl:otherwise></xsl:choose></h3>
          <xsl:if test="self::f:Encounter"><a href="/encounter/{f:id/@value}">Open encounter report →</a></xsl:if></header>
          <div class="resource-summary"><span>Record ID: <xsl:value-of select="f:id/@value"/></span><xsl:if test="f:status/@value"><span class="status-chip"><xsl:value-of select="f:status/@value"/></span></xsl:if><span><xsl:value-of select="f:effectiveDateTime/@value | f:period/f:start/@value | f:authoredOn/@value | f:recordedDate/@value | f:date/@value"/></span></div>
          <xsl:if test="not($reportText) and (f:valueQuantity or f:valueString or f:valueCodeableConcept or f:valueBoolean or f:valueInteger)"><p class="observation-value"><xsl:value-of select="f:valueQuantity/f:comparator/@value"/><xsl:value-of select="f:valueQuantity/f:value/@value | f:valueString/@value | f:valueBoolean/@value | f:valueInteger/@value"/><xsl:text> </xsl:text><xsl:choose><xsl:when test="f:valueQuantity/f:unit/@value"><xsl:value-of select="f:valueQuantity/f:unit/@value"/></xsl:when><xsl:otherwise><xsl:value-of select="f:valueQuantity/f:code/@value"/></xsl:otherwise></xsl:choose><xsl:choose><xsl:when test="f:valueCodeableConcept/f:text/@value"><xsl:value-of select="f:valueCodeableConcept/f:text/@value"/></xsl:when><xsl:otherwise><xsl:value-of select="f:valueCodeableConcept/f:coding[1]/f:display/@value"/></xsl:otherwise></xsl:choose></p></xsl:if>
          <xsl:for-each select="$reportText"><div class="clinical-document"><xsl:call-template name="render-report-lines"><xsl:with-param name="text" select="."/></xsl:call-template></div></xsl:for-each>
          <details class="resource-all-fields"><summary>All record details</summary><dl class="resource-fields"><xsl:apply-templates select="*" mode="field"/></dl></details>
        </article>
      </xsl:for-each>
    </div>
  </xsl:template>
  <!-- Generic recursive fallback preserves every field, choice type, extension and contained resource.
       Add resource-specific templates in this stylesheet to customize clinical presentation.
       Narrative is rendered as text; server-provided HTML and URLs are never executed. -->
  <xsl:template match="*" mode="field">
    <div class="resource-field"><dt><xsl:value-of select="local-name()"/></dt><dd>
      <xsl:for-each select="@*"><span class="field-value"><xsl:if test="local-name() != 'value'"><strong><xsl:value-of select="local-name()"/>: </strong></xsl:if><xsl:value-of select="."/></span></xsl:for-each>
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
