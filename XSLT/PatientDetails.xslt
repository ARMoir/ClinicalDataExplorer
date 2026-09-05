<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:f="http://hl7.org/fhir" exclude-result-prefixes="f">
  <xsl:output method="html" omit-xml-declaration="yes"/>
  <xsl:param name="facilityName"/><xsl:param name="logoPath"/><xsl:param name="primaryColor"/><xsl:param name="secondaryColor"/>
  <xsl:template match="/f:Patient">
    <section class="xslt-report xslt-patient-details">
      <div class="xslt-report-heading"><div><span class="eyebrow">Patient</span><h2><xsl:for-each select="f:name[1]/f:given"><xsl:value-of select="@value"/> </xsl:for-each><xsl:value-of select="f:name[1]/f:family/@value"/></h2></div><span class="status-chip"><xsl:choose><xsl:when test="f:active/@value='false'">inactive</xsl:when><xsl:otherwise>active</xsl:otherwise></xsl:choose></span></div>
      <div class="xslt-detail-grid">
        <div><span class="label">FHIR ID</span><span class="value"><xsl:value-of select="f:id/@value"/></span></div>
        <div><span class="label">Date of birth</span><span class="value"><xsl:value-of select="f:birthDate/@value"/></span></div>
        <div><span class="label">Gender</span><span class="value"><xsl:value-of select="f:gender/@value"/></span></div>
        <div><span class="label">Phone</span><span class="value"><xsl:value-of select="f:telecom[f:system/@value='phone'][1]/f:value/@value"/></span></div>
        <div><span class="label">Email</span><span class="value"><xsl:value-of select="f:telecom[f:system/@value='email'][1]/f:value/@value"/></span></div>
        <div><span class="label">Identifiers</span><span class="value"><xsl:for-each select="f:identifier"><xsl:if test="position() &gt; 1"><br/></xsl:if><xsl:value-of select="f:type/f:text/@value"/><xsl:if test="f:type/f:text/@value">: </xsl:if><xsl:value-of select="f:value/@value"/></xsl:for-each></span></div>
        <div class="xslt-wide"><span class="label">Address</span><span class="value"><xsl:for-each select="f:address[1]/f:line"><xsl:value-of select="@value"/> </xsl:for-each><xsl:value-of select="f:address[1]/f:city/@value"/>, <xsl:value-of select="f:address[1]/f:state/@value"/> <xsl:value-of select="f:address[1]/f:postalCode/@value"/></span></div>
      </div>
    </section>
  </xsl:template>
</xsl:stylesheet>
