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
            if (typ.IsAbstract || typ.IsInterface)
            {
                throw new InvalidOperationException($"Cannot infer the concrete type from {typ.Name}");
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
                    var reg = new Regex(@"(?<!\\)""");
                    var objStr = obj.ToString();
                    if (reg.IsMatch(objStr))
                    {
                        sb.Append("\"" + reg.Replace(objStr, @"\""") + "\"");
                    }
                    else
                    {
                        sb.Append("\"" + objStr + "\"");
                    }
                    //var regx = Regex.Replace(obj.ToString(), @"(?< !\\)""", @"\\""");
                    //sb.Append("\"" + regx + "\"");
                }
                catch (Exception ex)
                {
                    sb.Append(ex.Message);
                }
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
                        sb.AppendLine($@"{member.Name} = {SimpleCreator(typ.GetProperty(member.Name).GetValue(obj, null), typ.GetProperty(member.Name).PropertyType)},");
                }
                sb.AppendLine("}");
            }
            return sb.ToString();
        }

    }

}
