<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:f="http://hl7.org/fhir" exclude-result-prefixes="f">
  <xsl:output method="html" omit-xml-declaration="yes"/>
  <xsl:param name="facilityName"/><xsl:param name="logoPath"/><xsl:param name="primaryColor"/><xsl:param name="secondaryColor"/>
  <xsl:template match="/">
    <section class="xslt-report"><div class="xslt-report-heading"><div><span class="eyebrow">Encounter report</span><h2>Encounters</h2></div><span><xsl:value-of select="count(f:Bundle/f:entry/f:resource/f:Encounter)"/> records</span></div>
      <div class="table-wrap" tabindex="0" role="region" aria-label="Patient encounters"><table class="xslt-table"><thead><tr><th scope="col">Date</th><th scope="col">Type</th><th scope="col">Class</th><th scope="col">Status</th><th scope="col">Location / provider</th></tr></thead><tbody>
      <xsl:for-each select="f:Bundle/f:entry/f:resource/f:Encounter"><tr>
        <td><a><xsl:attribute name="href">/encounter/<xsl:value-of select="f:id/@value"/></xsl:attribute><xsl:value-of select="f:period/f:start/@value"/></a></td>
        <td><xsl:value-of select="f:type[1]/f:text/@value | f:type[1]/f:coding[1]/f:display/@value"/></td><td><xsl:value-of select="f:class/f:display/@value | f:class/f:code/@value"/></td><td><span class="status-chip"><xsl:value-of select="f:status/@value"/></span></td>
        <td><xsl:value-of select="f:location[1]/f:location/f:display/@value | f:serviceProvider/f:display/@value"/></td></tr></xsl:for-each>
      </tbody></table></div>
    </section>
  </xsl:template>
</xsl:stylesheet>
