<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:f="http://hl7.org/fhir" exclude-result-prefixes="f">
  <xsl:output method="html" omit-xml-declaration="yes"/>
  <xsl:param name="facilityName"/><xsl:param name="logoPath"/><xsl:param name="primaryColor"/><xsl:param name="secondaryColor"/>
  <xsl:template match="/">
    <div class="resource-records" style="--primary-color:{$primaryColor};--secondary-color:{$secondaryColor}">
      <xsl:for-each select="f:Bundle/f:entry/f:resource/*">
        <article class="resource-record">
          <header><h3><xsl:choose><xsl:when test="f:code/f:text/@value"><xsl:value-of select="f:code/f:text/@value"/></xsl:when><xsl:when test="f:code/f:coding/f:display/@value"><xsl:value-of select="f:code/f:coding[1]/f:display/@value"/></xsl:when><xsl:when test="f:type/f:text/@value"><xsl:value-of select="f:type[1]/f:text/@value"/></xsl:when><xsl:when test="f:name/f:text/@value"><xsl:value-of select="f:name[1]/f:text/@value"/></xsl:when><xsl:when test="f:name/@value"><xsl:value-of select="f:name/@value"/></xsl:when><xsl:when test="f:title/@value"><xsl:value-of select="f:title/@value"/></xsl:when><xsl:otherwise><xsl:value-of select="local-name()"/></xsl:otherwise></xsl:choose></h3>
          <xsl:if test="self::f:Encounter"><a href="/encounter/{f:id/@value}">Open encounter report →</a></xsl:if></header>
          <div class="resource-summary"><span><xsl:value-of select="local-name()"/> / <xsl:value-of select="f:id/@value"/></span><xsl:if test="f:status/@value"><span class="status-chip"><xsl:value-of select="f:status/@value"/></span></xsl:if><span><xsl:value-of select="f:effectiveDateTime/@value | f:period/f:start/@value | f:authoredOn/@value | f:recordedDate/@value | f:date/@value"/></span></div>
          <xsl:if test="f:valueQuantity or f:valueString or f:valueCodeableConcept or f:valueBoolean or f:valueInteger"><p class="observation-value"><xsl:value-of select="f:valueQuantity/f:comparator/@value"/><xsl:value-of select="f:valueQuantity/f:value/@value | f:valueString/@value | f:valueBoolean/@value | f:valueInteger/@value"/><xsl:text> </xsl:text><xsl:choose><xsl:when test="f:valueQuantity/f:unit/@value"><xsl:value-of select="f:valueQuantity/f:unit/@value"/></xsl:when><xsl:otherwise><xsl:value-of select="f:valueQuantity/f:code/@value"/></xsl:otherwise></xsl:choose><xsl:choose><xsl:when test="f:valueCodeableConcept/f:text/@value"><xsl:value-of select="f:valueCodeableConcept/f:text/@value"/></xsl:when><xsl:otherwise><xsl:value-of select="f:valueCodeableConcept/f:coding[1]/f:display/@value"/></xsl:otherwise></xsl:choose></p></xsl:if>
          <details class="resource-all-fields"><summary>All FHIR fields</summary><dl class="resource-fields"><xsl:apply-templates select="*" mode="field"/></dl></details>
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
</xsl:stylesheet>
