using System;
using System.Collections.Generic;
using System.Linq;
using Esprima;
using Esprima.Ast;

namespace Feather.Analysis
{
    public static class Analyzer
    {
        public static Script ParseScript(string program)
        {
            var parser = new JavaScriptParser(program);
            return parser.ParseScript();
        }

        public static bool IsScriptValid(Script script)
        {
            return script.Body.Any(b => b.Type == Nodes.ClassDeclaration);
        }
        
        public static bool HasJSBehaviour(ScriptMeta scriptMeta)
        {
            return scriptMeta.Class.ExtendsJsBehaviour;
        }
        
        public static ScriptMeta AnalyzeScript(string rawScript)
        {
            var script = ParseScript(rawScript);
            if (!IsScriptValid(script))
            {
                throw new Exception("Can't parse an invalid script");
            }
            
            return new ScriptMeta
            {
                Imports = GetImportsFromScript(script),
                Class = GetClasses(script).First()
            };
        }

        public static ScriptMeta AnalyzeScript(Script script)
        {
            var functions = script.Body
                .FirstOrDefault(b => b.Type == Nodes.ClassDeclaration)
                ?.As<ClassDeclaration>()
                .Body.ChildNodes.Where(n => n.Type == Nodes.MethodDefinition)
                .Cast<MethodDefinition>()
                .Select(md => md.ToString())
                .ToList();
            
            return new ScriptMeta
            {
                Imports = GetImportsFromScript(script),
                Class = GetClasses(script).First()
            };
        }
        
        private static List<string> GetImportsFromScript(Script script)
        {
            var imports = new List<string>();
            var variables = script.Body.FirstOrDefault(b => b.Type == Nodes.VariableDeclaration)?.As<VariableDeclaration>();
            if (variables == null)
            {
                return imports;
            }
            
            foreach (var variableDeclarator in variables.Declarations)
            {
                var ident = variableDeclarator.Init.ChildNodes.FirstOrDefault(c => c is Identifier).As<Identifier>();
                if (!(ident is { Name: "importNamespace" }))
                {
                    continue;
                }
            
                var literal = variableDeclarator.Init.ChildNodes.FirstOrDefault(c => c is Literal).As<Literal>();
                if (literal == null)
                {
                    continue;
                }
                
                imports.Add(literal.Raw);
            }

            return imports;
        }

        private static List<ClassMeta> GetClasses(Script script)
        {
            return script.Body
                .Where(b => b.Type == Nodes.ClassDeclaration)
                .Cast<ClassDeclaration>()
                .Select(classDeclaration => 
                    new ClassMeta
                    { 
                        Name = classDeclaration.Id.Name,
                        ExtendsJsBehaviour = classDeclaration.SuperClass?.ToString() == "jsBehaviour",
                        Properties = GetClassProperties(classDeclaration.Body),
                        Methods = GetClassMethods(classDeclaration.Body)
                    })
                .ToList();
        }

        private static List<Property> GetClassProperties(ClassBody classBody)
        {
            var propertyDefinitions = classBody.Body
                .Where(cn => cn.Type == Nodes.PropertyDefinition).Cast<PropertyDefinition>()
                .Where(pd => pd.Decorators.Any())
                .ToList();

            return propertyDefinitions
                .Select(classElement => 
                    new Property
                    {
                        Decorator = classElement.Decorators.First().Expression.ToString(), 
                        Name = ((Identifier)classElement.Key).Name
                    })
                .ToList();
        }
        
        private static List<string> GetClassMethods(ClassBody classBody)
        {
            return classBody.
                ChildNodes.Where(n => n.Type == Nodes.MethodDefinition)
                .Cast<MethodDefinition>()
                .Select(md => md.Key.ToString())
                .ToList();
        }
    }
}