<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:f="http://hl7.org/fhir" exclude-result-prefixes="f">
  <xsl:output method="html" omit-xml-declaration="yes"/>
  <xsl:param name="facilityName"/><xsl:param name="logoPath"/><xsl:param name="primaryColor"/><xsl:param name="secondaryColor"/>
  <xsl:template match="/f:Patient">
    <section class="xslt-report xslt-patient-details" style="--primary-color:{$primaryColor};--secondary-color:{$secondaryColor};border-top:4px solid {$primaryColor}">
      <div class="patient-report-brand"><xsl:if test="string-length($logoPath) &gt; 0"><img src="{$logoPath}" alt="{$facilityName}"/></xsl:if><strong style="color:{$secondaryColor}"><xsl:value-of select="$facilityName"/></strong></div>
      <div class="xslt-report-heading"><div><span class="eyebrow">Patient</span><h2>
        <xsl:choose>
          <xsl:when test="f:name[f:use/@value='official']"><xsl:apply-templates select="f:name[f:use/@value='official'][1]" mode="name"/></xsl:when>
          <xsl:when test="f:name[f:use/@value='usual']"><xsl:apply-templates select="f:name[f:use/@value='usual'][1]" mode="name"/></xsl:when>
          <xsl:when test="f:name"><xsl:apply-templates select="f:name[1]" mode="name"/></xsl:when>
          <xsl:otherwise>Unnamed patient</xsl:otherwise>
        </xsl:choose>
      </h2></div><span class="status-chip"><xsl:choose><xsl:when test="f:active/@value='false'">inactive</xsl:when><xsl:when test="f:active/@value='true'">active</xsl:when><xsl:otherwise>Not recorded</xsl:otherwise></xsl:choose></span></div>
      <div class="xslt-detail-grid">
        <div><span class="label">FHIR ID</span><span class="value"><xsl:call-template name="value"><xsl:with-param name="value" select="f:id/@value"/></xsl:call-template></span></div>
        <div><span class="label">Date of birth</span><span class="value"><xsl:call-template name="value"><xsl:with-param name="value" select="f:birthDate/@value"/></xsl:call-template></span></div>
        <div><span class="label">Gender</span><span class="value"><xsl:call-template name="value"><xsl:with-param name="value" select="f:gender/@value"/></xsl:call-template></span></div>
        <div><span class="label">Phone</span><span class="value"><xsl:call-template name="value"><xsl:with-param name="value" select="f:telecom[f:system/@value='phone'][1]/f:value/@value"/></xsl:call-template></span></div>
        <div><span class="label">Email</span><span class="value"><xsl:call-template name="value"><xsl:with-param name="value" select="f:telecom[f:system/@value='email'][1]/f:value/@value"/></xsl:call-template></span></div>
        <div><span class="label">Identifiers</span><span class="value"><xsl:choose><xsl:when test="f:identifier/f:value/@value"><xsl:for-each select="f:identifier[f:value/@value]"><xsl:if test="position() &gt; 1"><br/></xsl:if><xsl:if test="f:type/f:text/@value"><xsl:value-of select="f:type/f:text/@value"/>: </xsl:if><xsl:value-of select="f:value/@value"/><xsl:if test="f:system/@value"><small><xsl:value-of select="f:system/@value"/></small></xsl:if></xsl:for-each></xsl:when><xsl:otherwise>Not recorded</xsl:otherwise></xsl:choose></span></div>
        <div class="xslt-wide"><span class="label">Address</span><span class="value"><xsl:choose><xsl:when test="f:address[1]/f:text/@value"><xsl:value-of select="f:address[1]/f:text/@value"/></xsl:when><xsl:when test="f:address[1]/*/@value"><xsl:for-each select="f:address[1]/f:line | f:address[1]/f:city | f:address[1]/f:state | f:address[1]/f:postalCode | f:address[1]/f:country"><xsl:if test="position() &gt; 1"><xsl:text>, </xsl:text></xsl:if><xsl:value-of select="@value"/></xsl:for-each></xsl:when><xsl:otherwise>Not recorded</xsl:otherwise></xsl:choose></span></div>
      </div>
    </section>
  </xsl:template>
  <xsl:template match="f:name" mode="name"><xsl:choose><xsl:when test="f:given or f:family"><xsl:for-each select="f:given"><xsl:value-of select="@value"/><xsl:text> </xsl:text></xsl:for-each><xsl:value-of select="f:family/@value"/></xsl:when><xsl:when test="f:text/@value"><xsl:value-of select="f:text/@value"/></xsl:when><xsl:otherwise>Unnamed patient</xsl:otherwise></xsl:choose></xsl:template>
  <xsl:template name="value"><xsl:param name="value"/><xsl:choose><xsl:when test="string-length($value) &gt; 0"><xsl:value-of select="$value"/></xsl:when><xsl:otherwise>Not recorded</xsl:otherwise></xsl:choose></xsl:template>
</xsl:stylesheet>
