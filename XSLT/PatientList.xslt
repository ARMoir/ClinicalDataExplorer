<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:f="http://hl7.org/fhir" exclude-result-prefixes="f">
  <xsl:output method="html" omit-xml-declaration="yes"/>
  <xsl:param name="facilityName"/><xsl:param name="logoPath"/><xsl:param name="primaryColor"/><xsl:param name="secondaryColor"/>
  <xsl:template match="/">
    <section class="xslt-report xslt-patient-list">
      <div class="xslt-report-heading"><div><span class="eyebrow">Patient report</span><h2>Patients</h2></div><span><xsl:value-of select="count(f:Bundle/f:entry/f:resource/f:Patient)"/> records</span></div>
      <div class="table-wrap"><table class="xslt-table"><thead><tr><th>Name</th><th>Identifier</th><th>Date of birth</th><th>Gender</th><th>Updated</th></tr></thead><tbody>
        <xsl:for-each select="f:Bundle/f:entry/f:resource/f:Patient">
          <tr><td><a><xsl:attribute name="href">/patient/<xsl:value-of select="f:id/@value"/></xsl:attribute><strong><xsl:call-template name="name"/></strong></a></td>
          <td><xsl:for-each select="f:identifier"><xsl:if test="position() &gt; 1"> · </xsl:if><xsl:value-of select="f:value/@value"/></xsl:for-each></td>
          <td><xsl:value-of select="f:birthDate/@value"/></td><td><xsl:value-of select="f:gender/@value"/></td><td><xsl:value-of select="f:meta/f:lastUpdated/@value"/></td></tr>
        </xsl:for-each>
      </tbody></table></div>
    </section>
  </xsl:template>
  <xsl:template name="name"><xsl:variable name="n" select="f:name[f:use/@value='official'][1] | f:name[1][not(../f:name/f:use/@value='official')]"/><xsl:for-each select="$n/f:given"><xsl:value-of select="@value"/><xsl:text> </xsl:text></xsl:for-each><xsl:value-of select="$n/f:family/@value"/></xsl:template>
</xsl:stylesheet>
