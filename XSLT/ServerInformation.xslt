<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:f="http://hl7.org/fhir" exclude-result-prefixes="f">
  <xsl:output method="html" omit-xml-declaration="yes"/>
  <xsl:param name="facilityName"/><xsl:param name="logoPath"/><xsl:param name="primaryColor"/><xsl:param name="secondaryColor"/><xsl:param name="viewTitle">Reference information</xsl:param>
  <xsl:template match="/">
    <div class="server-information" style="--primary-color:{$primaryColor};--secondary-color:{$secondaryColor}">
      <xsl:apply-templates select="f:CapabilityStatement | f:TerminologyCapabilities | f:Bundle/f:entry/f:resource/*[not(self::f:OperationOutcome)]" mode="record"/>
    </div>
  </xsl:template>
  <xsl:template match="*" mode="record">
    <article class="reference-record">
      <h3><xsl:choose><xsl:when test="f:title/@value"><xsl:value-of select="f:title/@value"/></xsl:when><xsl:when test="f:name/@value"><xsl:value-of select="f:name/@value"/></xsl:when><xsl:when test="f:name/f:text/@value"><xsl:value-of select="f:name[1]/f:text/@value"/></xsl:when><xsl:otherwise><xsl:value-of select="$viewTitle"/></xsl:otherwise></xsl:choose></h3>
      <xsl:if test="f:description/@value | f:implementation/f:description/@value"><p><xsl:value-of select="f:description/@value | f:implementation/f:description/@value"/></p></xsl:if>
      <dl class="reference-summary">
        <xsl:if test="f:publisher/@value"><div><dt>Publisher</dt><dd><xsl:value-of select="f:publisher/@value"/></dd></div></xsl:if>
        <xsl:if test="f:software"><div><dt>Software</dt><dd><xsl:value-of select="f:software/f:name/@value"/><xsl:text> </xsl:text><xsl:value-of select="f:software/f:version/@value"/></dd></div></xsl:if>
        <xsl:if test="f:status/@value"><div><dt>Status</dt><dd><xsl:value-of select="f:status/@value"/></dd></div></xsl:if>
        <xsl:if test="f:date/@value"><div><dt>Published</dt><dd><xsl:value-of select="f:date/@value"/></dd></div></xsl:if>
        <xsl:if test="f:version/@value"><div><dt>Version</dt><dd><xsl:value-of select="f:version/@value"/></dd></div></xsl:if>
        <xsl:if test="f:id/@value"><div><dt>Record ID</dt><dd><xsl:value-of select="f:id/@value"/></dd></div></xsl:if>
        <xsl:if test="self::f:CapabilityStatement"><div><dt>Supported record categories</dt><dd><xsl:value-of select="count(f:rest[f:mode/@value='server']/f:resource)"/></dd></div></xsl:if>
        <xsl:if test="self::f:TerminologyCapabilities"><div><dt>Declared code systems</dt><dd><xsl:value-of select="count(f:codeSystem)"/></dd></div></xsl:if>
      </dl>
      <xsl:if test="f:codeSystem"><details><summary>Supported code systems (<xsl:value-of select="count(f:codeSystem)"/>)</summary><ul><xsl:for-each select="f:codeSystem"><li><xsl:value-of select="f:uri/@value"/><xsl:for-each select="f:version"><xsl:text> · </xsl:text><xsl:value-of select="f:code/@value"/></xsl:for-each></li></xsl:for-each></ul></details></xsl:if>
      <details class="reference-details"><summary>Technical details</summary><dl class="resource-fields"><xsl:apply-templates select="*" mode="field"/></dl></details>
    </article>
  </xsl:template>
  <!-- Keep original names and all nested values available for troubleshooting.
       Remote markup and URLs are text only, never executable HTML or links. -->
  <xsl:template match="*" mode="field">
    <div class="resource-field"><dt><xsl:value-of select="local-name()"/></dt><dd>
      <xsl:for-each select="@*"><span class="field-value"><xsl:if test="local-name() != 'value'"><strong><xsl:value-of select="local-name()"/>: </strong></xsl:if><xsl:value-of select="."/></span></xsl:for-each>
      <xsl:choose><xsl:when test="namespace-uri()='http://www.w3.org/1999/xhtml'"><xsl:value-of select="."/></xsl:when><xsl:when test="*"><dl><xsl:apply-templates select="*" mode="field"/></dl></xsl:when><xsl:otherwise><xsl:value-of select="text()"/></xsl:otherwise></xsl:choose>
    </dd></div>
  </xsl:template>
</xsl:stylesheet>
