<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    <xsl:output method="html"
        media-type="text/html"
        doctype-public="-//W3C//DTD HTML 4.0 Transitional//EN"
        indent="yes"
        encoding="utf-8"/>
    
    <xsl:include href="include-xmlView.xsl"/>
    
    <xsl:template match="/">
			<link rel="stylesheet" href="../css/style.css" />
        <div class="xmlPreview">
            <xsl:call-template name="root" />
        </div>
    </xsl:template>
</xsl:stylesheet>    
