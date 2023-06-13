using Fluid.Ast;
using Newtonsoft.Json;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Authentication.ExtendedProtection;
using System.Text.RegularExpressions;


namespace JsonSchemaGenerator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string inSchemaPath = (args == null || args.Length < 1) ? "schema.json" : args[0];
            string outRoot = (args == null || args.Length < 2) ? AppDomain.CurrentDomain.BaseDirectory : args[1];
            string outFileName = (args == null || args.Length < 3) ? "GeneratedClasses.cs" : args[2];
            var schema = JsonSchema.FromFileAsync(inSchemaPath).Result;
            var settings = new CSharpGeneratorSettings();
            settings.GenerateDataAnnotations = false;
            settings.Namespace = "AutomaticGenerators";
            settings.GenerateJsonMethods = false;
            settings.ArrayBaseType = "System.Collections.Generic.IList";
            settings.ArrayType = "System.Collections.Generic.IList";
            var generator = new CSharpGenerator(schema, settings);
            var code = generator.GenerateFile();
            code = Regex.Replace(code, @".*\[Newtonsoft.*\]\r?\n", string.Empty);
            code = Regex.Replace(code, @".*\[System.*\]\r?\n", string.Empty);
            string[] lines = code.Split('\n');

            // re-write property shortcuts
            var toBeWritten = new string[lines.Length];
            int i = 0;
            foreach (var line in lines)
            {
                if (IsConvertableProperty(line))
                {
                    var thisVar = new FullPropMeta(line);
                    toBeWritten[i++] = thisVar.GenerateFullProp();
                }
                else { toBeWritten[i++] = line; }
            }
            File.WriteAllLines(outRoot + "\\" + outFileName, toBeWritten);

            // Generate partial classes with IObservable methods
            bool inClassBlock = false;
            int bracketCounter = 0;
            List<string> propList = new List<string>();
            SchemaClass schemaClass = new SchemaClass();
            foreach (var line in lines)
            {
                if (!inClassBlock) // Allow the code to enter a class block
                {
                    if (SchemaClass.IsClassDefinition(line)) // detect the class definition line and create the class
                    {
                        schemaClass = new SchemaClass(line);
                        propList = new List<string>();
                        inClassBlock = true;
                    }
                }
                else // Inside a class block keep track of brackets and parse properties
                {
                    // Assume the very next line has a "{" and brackets seem to always be in a single line
                    if (line.Trim() == "{") { bracketCounter++; }
                    else if (line.Trim() == "}") { bracketCounter--; }
                    else 
                    {
                        var _prop = SchemaClass.TryParseClassProperty(line);
                        if (_prop != string.Empty) { propList.Add(_prop); }
                    }

                    if (bracketCounter == 0)
                    {
                        inClassBlock = false;
                        schemaClass.Properties = propList;
                        schemaClass.BuildBonsaiOperator(outRoot);
                    }
                }
            }
        }

        private static bool IsConvertableProperty(string line)
        {
            var regexString = "^[A-Za-z]+\\s+[A-Za-z0-9_.<>]+\\s+[A-Za-z0-9_.<>]+ \\{ get; set; \\}\\s+=(.*);$";
            return Regex.IsMatch(line.Trim(), regexString, RegexOptions.IgnoreCase);
            
        }
    }


    public class FullPropMeta
    {
        public string AccessModifier;
        public string Type;
        public string Name;
        public string DefaultValue;
        public string LeadingWhite;

        public FullPropMeta(string shortcutPropString)
        {
            var splitString = shortcutPropString.Trim().Split(' ');
            AccessModifier = splitString[0];
            Type = splitString[1];
            Name = splitString[2];
            DefaultValue = shortcutPropString.Contains("=") ? shortcutPropString.Split('=')[1].Trim(';').Trim() : null;
            LeadingWhite = string.Concat(shortcutPropString.TakeWhile(Char.IsWhiteSpace)).Replace("    ", "\t");
        }

        public string GenerateFullProp()
        {
            const string template = "{5}private {1} {3} = {4};\n{5}{0} {1} {2}\n{5}{{\n{5}\tget {{ return {3}; }}\n{5}\tset {{ {3} = value; }}\n{5}}}";
            return string.Format(template,
                this.AccessModifier,
                this.Type,
                ToUpperFirstChar(this.Name),
                ToLowerFirstChar(this.Name),
                this.DefaultValue,
                this.LeadingWhite);
            
        }
        public static string ToLowerFirstChar(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            return char.ToLower(input[0]) + input.Substring(1);
        }
        public static string ToUpperFirstChar(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            return char.ToUpper(input[0]) + input.Substring(1);
        }
    }

    public class SchemaClass
    {
        public string Name { get; set; }
        public List<string> Properties { get; set; }

        public SchemaClass()
        {

        }
        public SchemaClass(string line)
        {
            var splitString = line.Trim().Split(' ');
            Name = splitString[3];
        }

        public static bool IsClassDefinition(string line) {
            string regexpString = "^public partial class\\s+(.*?)$";
            return Regex.IsMatch(line.Trim(), regexpString, RegexOptions.IgnoreCase);
        }

        public static string TryParseClassProperty(string line)
        {
            const string regexpStringNoGetSet = "^public\\s(.*?)\\s[A-Za-z0-9_]+$";
            const string regexpStringWithGetSet = "^public\\s(.*?)\\s[A-Za-z0-9_]+\\s{ get; set; }(.*?)$";

            var propName = string.Empty;
            if (Regex.IsMatch(line.Trim(), regexpStringNoGetSet, RegexOptions.IgnoreCase))
            {
                var splits = line.Trim().Trim(';').Split(' ');
                propName = splits[splits.Length - 1];
            }

            else if (Regex.IsMatch(line.Trim(), regexpStringWithGetSet, RegexOptions.IgnoreCase))
            {
                var splits = line.Trim().Split(' ');
                propName = splits[2];
                var i = 2;
                while (!propName.All(c => char.IsLetterOrDigit(c) | c == '_'))
                {
                    propName = splits[++i];
                }
            }
            Console.WriteLine(propName);

            return propName;
        }

        public string formatProperties()
        {
            var fprop = string.Empty;
            foreach(var p in this.Properties)
            {
                fprop += string.Format("\t\t\t\t\t{0} = {0},\n", p);
            }
            return fprop;
        }

        public string formatIObservable()
        {
            return string.Format(template,
                "AutomaticGenerators", this.Name, this.formatProperties());
        }

        public void BuildBonsaiOperator(string rootPath = "", string filename = null) { 
        
            if (filename == null)
            {
                filename = rootPath + "\\" + this.Name + ".cs";
            }
            File.WriteAllText(filename, this.formatIObservable());
        }

        private readonly string template =
@"
using Bonsai;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using YamlDotNet.Serialization;
using System.IO;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Core;
using {0};

namespace {0}
{{
    [Combinator]
    [Description(""Constructor."")]
    [WorkflowElementCategory(ElementCategory.Source)]
    public partial class {1}
    {{
        public IObservable<{1}> Process()
        {{
            return Observable.Defer(() =>
            {{
                var value = new {1}
                {{
{2}
                }};
                return Observable.Return(value);
            }});
        }}
    }}
}}
";

    }




}

