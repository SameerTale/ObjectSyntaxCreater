using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ModelClassInstanceSyntaxCreator
{

        public class InstanceSyntaxCreator
    {
        public static string SimpleCreator(object obj, Type typ)
        {
            if (!(typ.IsGenericType && typ.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                if (typ.IsAbstract || typ.IsInterface)
                {
                    throw new InvalidOperationException($"Cannot infer the concrete type from {typ.Name}");
                }
            }
            if (obj == null)
            {
                return " null";
            }
            var sb = new StringBuilder();
            if (typ.IsArray)
            {
                sb.Append($" new {typ.GetElementType()}[] {{");
                sb.AppendLine();
                object[] array = ((Array)((object)obj)).Cast<object>().ToArray();
                foreach (var elem in array)
                {
                    sb.Append($@"{SimpleCreator(elem, typ.GetElementType())},");
                }
                sb.AppendLine("}");
            }
            else if (typ.IsGenericType && (typ.GetGenericTypeDefinition() == typeof(List<>) || typ.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                var itemType = typ.GetGenericArguments()[0];
                var collection = (IEnumerable)obj;
                sb.Append($" new List<{itemType}>() {{");
                sb.AppendLine();
                foreach (var elem in collection)
                {
                    sb.Append($@"{SimpleCreator(elem, itemType)},");
                }
                sb.AppendLine("}");
            }
            else if (typ.IsGenericType && (typ.GetGenericTypeDefinition() == typeof(Dictionary<,>) || typ.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
            {
                var itemType1 = typ.GetGenericArguments()[0];
                var itemType2 = typ.GetGenericArguments()[1];
                var collection = (IEnumerable)obj;
                sb.Append($" new Dictionary<{itemType1},{itemType2}>() {{");
                sb.AppendLine();
                foreach (var elem in collection)
                {
                    var key = elem.GetType().GetProperty("Value").GetValue(elem, null);
                    var val = elem.GetType().GetProperty("Value").GetValue(elem, null);
                    sb.Append($@"{{");
                    sb.Append($@"{SimpleCreator(key, itemType1)}, {SimpleCreator(val, itemType2)}");
                    sb.Append("},");
                }
                sb.AppendLine("}");
            }
            else if (typ.IsEnum)
            {
                sb.Append($"{typ.Name}.{typ.GetEnumName(obj)}");
            }
            else if (typ == typeof(int) || typ == typeof(bool))
                sb.Append(obj.ToString().ToLower());
            else if (typ == typeof(string))
            {
                try
                {
                    var objStr = JsonConvert.ToString(obj);
                    sb.Append(objStr);
                }
                catch (Exception ex)
                {
                    sb.Append(ex.Message);
                }
            }
            else if (typ == typeof(DateTime) || typ == typeof(Nullable<DateTime>))
            {
                if (obj != null)
                    sb.Append("DateTime.Parse(\"" + obj.ToString() + "\")");
                else
                    sb.Append("null");
            }
            else if (typ == typeof(byte))
            {
                sb.Append(Convert.ToInt32(obj).ToString());
            }
            else if (typ == typeof(char))
            {
                sb.Append("'" + obj.ToString() + "'");
            }
            else if (typ.IsValueType)
                sb.Append(obj.ToString());
            else if (typ.IsClass)
            {
                //var constrs = typ.GetConstructors();
                //if (constrs == null || constrs.Length != 1)
                //{
                //    throw new InvalidOperationException($"Cannot decide the constructor from {typ.Name}");
                //}
                sb.Append($"new {typ.Name} {{");
                sb.AppendLine();
                foreach (var member in typ.GetMembers())
                {
                    if (member.MemberType == System.Reflection.MemberTypes.Property || member.MemberType == System.Reflection.MemberTypes.Field || member.MemberType == System.Reflection.MemberTypes.NestedType)
                    {
                        sb.AppendLine($@"{member.Name} = {SimpleCreator(typ.GetProperty(member.Name).GetValue(obj, null), typ.GetProperty(member.Name).PropertyType)},");
                    }
                }
                sb.AppendLine("}");
            }
            return sb.ToString();
        }


        private static Dictionary<string, string> FunctionsList { get; set; }
        public static string ObjectComparerSyntax(Type typ)
        {
            if (typ.IsAbstract || typ.IsInterface || !typ.IsClass)
            {
                throw new InvalidOperationException($"Cannot infer the concrete type from {typ.Name}");
            }
            FunctionsList = new Dictionary<string, string>();
            ObjectValueComparer(typ);
            var sb = new StringBuilder();
            foreach (var fn in FunctionsList)
            {
                sb.AppendLine(fn.Value);
            }
            FunctionsList = null;
            return sb.ToString();
        }

        private static string ObjectValueComparer(Type typ)
        {
            if (typ.IsAbstract || typ.IsInterface)
            {
                throw new InvalidOperationException($"Cannot infer the concrete type from {typ.Name}");
            }
            //if (obj1 == null || obj2 == null)
            //{
            //    return " null";
            //}
            if (typ.IsArray)
            {
                Type elemType = typ.GetElementType();
                bool isElemPrimitive = (elemType.IsValueType || elemType.IsEnum || elemType == typeof(int) || elemType == typeof(bool) || elemType == typeof(string));
                string childTypName = typ.GetElementType().Name;
                //string fnName = typName + "ComparerArr";
                string childFnName = ObjectValueComparer(typ.GetElementType());
                string fnName = childFnName + "Arr";
                string condn = "";
                if (isElemPrimitive)
                {
                    condn = "obj1[i] == obj2[i]";
                }
                else
                {
                    condn = $"{childFnName}(obj1[i], obj2[i])";
                }
                if (FunctionsList.ContainsKey(fnName))
                    return fnName;
                else
                    FunctionsList.Add(fnName, "");
                string arrComp = $@"
        public bool {fnName} ({childTypName}[] obj1, {childTypName}[] obj2) {{
            if (obj1 == null && obj2 == null)
                return true;
            if (obj1 == null || obj2 == null)
                return false;
            if (obj1.Length != obj2.Length)
                return false;
            for(int i = 0; i < obj1.Length;  i++)
            {{
                if (!({condn}))
                    return false;
            }}
            return true;
        }}
";
                if (string.IsNullOrEmpty(FunctionsList[fnName]))
                    FunctionsList[fnName] = arrComp;
                return fnName;
            }
            else if (typ.IsEnum || typ == typeof(int) || typ == typeof(bool) || typ == typeof(string) || typ.IsValueType)
            {
                return typ.Name.Replace('.', '_') + "Comparer";
            }
            else if (typ.IsClass)
            {
                //var constrs = typ.GetConstructors();
                //if (constrs == null || constrs.Length != 1)
                //{
                //    throw new InvalidOperationException($"Cannot decide the constructor from {typ.Name}");
                //}
                //foreach (var member in typ.GetMembers())
                //{
                //    if (member.MemberType == System.Reflection.MemberTypes.Property || member.MemberType == System.Reflection.MemberTypes.Field || member.MemberType == System.Reflection.MemberTypes.NestedType)
                //        sb.AppendLine($@"{member.Name} = {SimpleCreator(typ.GetProperty(member.Name).GetValue(obj, null), typ.GetProperty(member.Name).PropertyType)},");
                //}
                string typName = typ.Name;
                string fnName = typName + "Comparer";
                if (FunctionsList.ContainsKey(fnName))
                    return fnName;
                else
                    FunctionsList.Add(fnName, "");
                StringBuilder sb = new StringBuilder();
                List<string> classMembers = new List<string>();

                foreach (var member in typ.GetMembers())
                {

                    if (member.MemberType == System.Reflection.MemberTypes.Property || member.MemberType == System.Reflection.MemberTypes.Field || member.MemberType == System.Reflection.MemberTypes.NestedType)
                    {
                        var propType = typ.GetProperty(member.Name).PropertyType;
                        if (propType.IsEnum || propType == typeof(int) || propType == typeof(bool) || propType == typeof(string) || propType.IsValueType)
                        {
                            classMembers.Add($"obj1.{member.Name} == obj2.{member.Name}");
                        }
                        else
                        {
                            //string innerFn;
                            //var memberType = typ.GetProperty(member.Name).PropertyType;
                            //if (memberType.IsArray)
                            //{
                            //    inner
                            //}

                            //var innerFn = typ.GetProperty(member.Name).PropertyType.Name + "Comparer";
                            //if (!FunctionsList.ContainsKey(innerFn))
                            var innerFn = ObjectValueComparer(typ.GetProperty(member.Name).PropertyType);
                            classMembers.Add($"{innerFn}(obj1.{member.Name}, obj2.{member.Name})");
                        }

                    }
                }
                if (classMembers.Count == 0)
                    sb.AppendLine("return true;");
                else
                {
                    sb.AppendLine("return (");
                    sb.AppendLine(string.Join(" &&" + '\r' + '\n', classMembers));
                    sb.AppendLine(");");
                }
                string fnSyntax = $@"
        public bool {fnName} ({typName} obj1, {typName} obj2) {{
                if (obj1 == null && obj2 == null)
                    return true;
                if (obj1 == null || obj2 == null)
                    return false;
                {sb.ToString()}
                }}";
                if (string.IsNullOrEmpty(FunctionsList[fnName]))
                    FunctionsList[fnName] = fnSyntax;
                return fnName;
            }
            return "";
        }

    }


}
