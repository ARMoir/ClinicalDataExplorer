<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:f="http://hl7.org/fhir" exclude-result-prefixes="f">
  <xsl:output method="html" omit-xml-declaration="yes"/>
  <xsl:param name="facilityName"/><xsl:param name="logoPath"/><xsl:param name="primaryColor"/><xsl:param name="secondaryColor"/>
  <xsl:template match="/f:Encounter">
    <section class="xslt-report"><div class="xslt-report-heading"><div><span class="eyebrow">Encounter</span><h2><xsl:value-of select="f:type[1]/f:text/@value | f:type[1]/f:coding[1]/f:display/@value"/></h2></div><span class="status-chip"><xsl:value-of select="f:status/@value"/></span></div>
      <div class="xslt-detail-grid">
        <div><span class="label">Start</span><span class="value"><xsl:value-of select="f:period/f:start/@value"/></span></div><div><span class="label">End</span><span class="value"><xsl:value-of select="f:period/f:end/@value"/></span></div>
        <div><span class="label">Class</span><span class="value"><xsl:value-of select="f:class/f:display/@value | f:class/f:code/@value"/></span></div><div><span class="label">Service provider</span><span class="value"><xsl:value-of select="f:serviceProvider/f:display/@value"/></span></div>
        <div><span class="label">Record ID</span><span class="value"><xsl:value-of select="f:id/@value"/></span></div><div><span class="label">Identifier</span><span class="value"><xsl:value-of select="f:identifier[1]/f:value/@value"/></span></div>
        <div class="xslt-wide"><span class="label">Reason</span><span class="value"><xsl:value-of select="f:reasonCode[1]/f:text/@value | f:reasonCode[1]/f:coding[1]/f:display/@value"/></span></div>
      </div>
    </section>
  </xsl:template>
</xsl:stylesheet>
