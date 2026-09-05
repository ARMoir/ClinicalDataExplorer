<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:f="http://hl7.org/fhir" exclude-result-prefixes="f">
  <xsl:output method="html" omit-xml-declaration="yes"/>
  <xsl:param name="facilityName"/><xsl:param name="logoPath"/><xsl:param name="primaryColor"/><xsl:param name="secondaryColor"/>
  <xsl:template match="/">
    <section class="xslt-report"><div class="xslt-report-heading"><div><span class="eyebrow">Observation report</span><h2>Observations</h2></div><span><xsl:value-of select="count(f:Bundle/f:entry/f:resource/f:Observation)"/> results</span></div>
      <div class="table-wrap"><table class="xslt-table"><thead><tr><th>Date</th><th>Observation</th><th>Value</th><th>Interpretation</th><th>Reference range</th><th>Status</th></tr></thead><tbody>
      <xsl:for-each select="f:Bundle/f:entry/f:resource/f:Observation"><tr>
        <td><xsl:value-of select="f:effectiveDateTime/@value | f:issued/@value"/></td><td><strong><xsl:value-of select="f:code/f:text/@value | f:code/f:coding[1]/f:display/@value"/></strong></td>
        <td class="observation-value"><xsl:choose><xsl:when test="f:valueQuantity"><xsl:value-of select="f:valueQuantity/f:value/@value"/> <xsl:value-of select="f:valueQuantity/f:unit/@value | f:valueQuantity/f:code/@value"/></xsl:when><xsl:when test="f:valueCodeableConcept"><xsl:value-of select="f:valueCodeableConcept/f:text/@value | f:valueCodeableConcept/f:coding[1]/f:display/@value"/></xsl:when><xsl:otherwise><xsl:value-of select="f:valueString/@value | f:valueCode/@value | f:valueDateTime/@value | f:valueInteger/@value | f:valueBoolean/@value"/></xsl:otherwise></xsl:choose></td>
        <td><xsl:value-of select="f:interpretation[1]/f:text/@value | f:interpretation[1]/f:coding[1]/f:display/@value"/></td><td><xsl:value-of select="f:referenceRange[1]/f:text/@value"/><xsl:if test="not(f:referenceRange[1]/f:text)"><xsl:value-of select="f:referenceRange[1]/f:low/f:value/@value"/>–<xsl:value-of select="f:referenceRange[1]/f:high/f:value/@value"/> <xsl:value-of select="f:referenceRange[1]/f:high/f:unit/@value"/></xsl:if></td><td><span class="status-chip"><xsl:value-of select="f:status/@value"/></span></td>
      </tr></xsl:for-each></tbody></table></div>
    </section>
  </xsl:template>
</xsl:stylesheet>
