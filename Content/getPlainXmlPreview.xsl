<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    <xsl:output method="xml"
        media-type="text/xml"
        indent="yes"
        encoding="utf-8"/>    

    <xsl:template match="/">
      <xsl:copy-of select="." />
    </xsl:template>
</xsl:stylesheet>    
