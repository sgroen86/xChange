<?xml version="1.0" encoding="UTF-8"?>
<!--
  Turns an SVRL report into one line per failed assert:
  flag <TAB> rule id <TAB> message
  so validate.sh can count fatals without parsing XML in shell.
-->
<xsl:stylesheet version="2.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:svrl="http://purl.oclc.org/dsdl/svrl">
  <xsl:output method="text" encoding="UTF-8"/>
  <xsl:template match="/">
    <xsl:for-each select="//svrl:failed-assert">
      <xsl:value-of select="concat(@flag, '&#9;', @id, '&#9;', normalize-space(svrl:text), '&#10;')"/>
    </xsl:for-each>
  </xsl:template>
</xsl:stylesheet>
