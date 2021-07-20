using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace Ingeniux.Runtime.Models.APIModels
{
    public class PageElementSettings
    {
        public PageElementSettings()
        {
            var appSettings = ConfigurationManager.AppSettings;

            string elementExceptionListValue = appSettings["ElementExceptionList"] ?? string.Empty;
            ElementExceptionList = elementExceptionListValue.Split(',').Select(v => v.Trim());

            string attributeExceptionListValue = appSettings["AttributeExceptionList"] ?? string.Empty;
            AttributeExceptionList = attributeExceptionListValue.Split(',').Select(v => v.Trim());

        }

        public IEnumerable<string> ElementExceptionList { get; set; }
        public IEnumerable<string> AttributeExceptionList { get; set; }
    }

    /*
  <appSettings>
    <add key="ElementExceptionList" value="Exception1,Exception2"/>
    <add key="AttributeExceptionList" value="Exception1,Exception2"/>
  </appSettings>
    */

    public class ElementModel
    {
        internal IEnumerable<string> ElementExceptionList
        {
            get
            {
                return Settings.ElementExceptionList;
            }
        }

        internal IEnumerable<string> AttributeExceptionList
        {
            get
            {
                return Settings.AttributeExceptionList;
            }
        }

        internal PageElementSettings _Settings { get; set; }
        internal PageElementSettings Settings
        {
            get
            {
                if (_Settings == null)
                {
                    _Settings = new PageElementSettings();
                }
                return _Settings;
            }
        }

        public string Name { get; set; }
        public string Value { get; set; }
        public string Type { get; set; }
        public IEnumerable<ElementModel> Children { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

        public ElementModel(ICMSElement element)
        {
            Attributes = element.Attributes().Where(a => !AttributeExceptionList.Contains(a.AttributeName)).ToDictionary(a => a.AttributeName, a => a.Value);
            Name = element.RootElementName;
            Value = element.Value;
            Children = element.Descendants().Where(e => !ElementExceptionList.Contains(e.RootElementName)).Select(e => new ElementModel(e));
        }

        public ElementModel()
        {

        }
    }
}