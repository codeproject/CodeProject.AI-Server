using System.Dynamic;
using System.Reflection;
using System.Text.Json;

namespace CodeProject.AI.SDK.Utils
{
    public static class ExpandoExtensions
    {
        /*
        public static void Add(ExpandoObject expandoObject, string name, object value)
        {
            var expandoDict = expandoObject as IDictionary<string, object>;
            if (expandoDict.ContainsKey(name))
                expandoDict[name] = value;
            else
                expandoDict.Add(name, value);
        }
        */

        /// <summary>
        /// Converts all property names in the ExpandoObject to camelCase
        /// </summary>
        /// <param name="expandoObject">The anonymous object</param>
        /// <returns>An ExpandoObject</returns>
        public static void ToCamelCase(this ExpandoObject expandoObject)
        {
            if (expandoObject is null)
                return;

            var dictionary = expandoObject as IDictionary<string, object>;
 
            var properties = new List<string>(dictionary.Keys);
            foreach (var propertyName in properties)
            {
                string key = JsonNamingPolicy.CamelCase.ConvertName(propertyName);
                if (key != propertyName)
                {
                    var value = dictionary[propertyName];
                    dictionary.Remove(propertyName);
                    dictionary[key] = value;
                }
            }
        }
        
        /// <summary>
        /// Converts an anonymous object to an ExpandoObject
        /// </summary>
        /// <param name="initialObj">The anonymous object</param>
        /// <returns>An ExpandoObject</returns>
        public static ExpandoObject ToExpando(this object initialObj)
        {
            ExpandoObject expandoObject = new ExpandoObject();
            IDictionary<string, object> dict = expandoObject!;

            Type objectType = initialObj.GetType();
            foreach(var prop in objectType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var value = prop.GetValue(initialObj);
                if (value is not null)
                    dict.Add(prop.Name, value);
            }

            return expandoObject;
        }

        public static ExpandoObject Merge(this ExpandoObject? left, ExpandoObject? right)
        {
            if (left is null) return right ?? new ExpandoObject();
            if (right is null) return left ?? new ExpandoObject();

            ExpandoObject expandoObject = new ExpandoObject();
            IDictionary<string, object> mergedDict = expandoObject!;

            var dict = left as IDictionary<string, object>;
            foreach (var key in dict.Keys)
            {
                if (mergedDict.ContainsKey(key))
                    mergedDict[key] = dict[key];
                else
                    mergedDict.Add(key, dict[key]);
            }

            dict = right as IDictionary<string, object>;
            foreach (var key in dict.Keys)
            {
                if (mergedDict.ContainsKey(key))
                    mergedDict[key] = dict[key];
                else
                    mergedDict.Add(key, dict[key]);
            }

            return expandoObject;
        }
    }
}