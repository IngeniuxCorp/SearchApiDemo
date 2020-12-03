<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    
    <xsl:template name="root">
        <xsl:param name="windowVersion" />
        <span class="tag" style="margin-left:10px;">
            &lt;?xml version="1.0" ?&gt; 
        </span>                
        <div >
            <xsl:if test="$windowVersion">
                <xsl:attribute name="id">root</xsl:attribute>
            </xsl:if>
            <xsl:apply-templates>
                <xsl:with-param name="depth" select="1" />
            </xsl:apply-templates>
        </div>        
    </xsl:template>
    
    <xsl:template match="*">
        <xsl:param name="depth" />
        
        <span class="toggle">
            <xsl:choose>
                <xsl:when test="normalize-space(.) != '' or count(*) != 0">
                    <xsl:text>-</xsl:text>                         
                </xsl:when>
                <xsl:otherwise>
                    <xsl:text></xsl:text>
                </xsl:otherwise>
            </xsl:choose>
        </span>         
        
        <div style="" class="line">
            <xsl:call-template name="writeElementTag"/>
            <div class="container">
                <xsl:apply-templates>
                    <xsl:with-param name="depth" select="$depth + 2" />
                </xsl:apply-templates>  
            </div>
        </div>
        
        <xsl:if test="normalize-space(.) != '' or count(*) != 0">
            <span class="toggle">
                <xsl:text> </xsl:text>        
            </span>               
            <div  class="lineEnd">
                <xsl:call-template name="writeElementTag">
                    <xsl:with-param name="pos" select="'end'" />
                </xsl:call-template>
            </div>
        </xsl:if>
    </xsl:template>
    
    <xsl:template match="comment()">
        <div style="margin-left:10px;">
            <span class="tag">&lt;--  </span>
            <span class="comment">
                <xsl:value-of select="."/>
            </span>        
            <span class="tag">  --&gt;</span>
        </div>
    </xsl:template>
    
    <xsl:template match="@*">
        <span class="name"><xsl:text> </xsl:text><xsl:value-of select="name()"/></span>
        <span class="tag">="</span>
        <xsl:value-of select="."/>
        <span class="tag">"</span>        
    </xsl:template>
    
    <xsl:template name="writeElementTag">
        <xsl:param name="pos" />
        
        <xsl:param name="parentNameSpaces" select="parent::*/namespace::*[. != 'http://www.w3.org/XML/1998/namespace']" />
        
        <span class="tag">&lt;</span>
        <xsl:if test="$pos = 'end'">
            <xsl:text>/</xsl:text>
        </xsl:if>
        <span class="name"><xsl:value-of select="name()"/></span>
        <xsl:if test="$pos != 'end'">
            <xsl:for-each select="namespace::*[. != 'http://www.w3.org/XML/1998/namespace']" >
                <xsl:variable name="nsValue" select="." />
                <xsl:if test="not($parentNameSpaces[. = $nsValue])">
                    <span class="name"><xsl:text> xmlns</xsl:text>
                        <xsl:if test="normalize-space(name()) != ''">
                            <xsl:text>:</xsl:text>
                            <xsl:value-of select="name()"/>
                        </xsl:if>
                    </span>
                    <span class="tag">="</span>
                    <xsl:value-of select="."/>
                    <span class="tag">"</span>                         
                </xsl:if>              
            </xsl:for-each>
            <xsl:apply-templates select="@*" />
        </xsl:if>
        <xsl:if test="normalize-space(.) = '' and count(*) = 0">
            <span class="tag"><xsl:text> /</xsl:text></span>
        </xsl:if>
        <span class="tag"><xsl:text>&gt;</xsl:text></span>
    </xsl:template>
    
</xsl:stylesheet>