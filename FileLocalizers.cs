using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace PseudolocalizeUWP
{
    /// <summary>
    /// 把原始项目里针对各种文本格式的命令移植成"接收文本、返回文本"的纯函数，
    /// 方便 UWP 里只做预览。对应关系：
    ///   .txt  -> file 命令（逐行伪本地化）
    ///   .inf  -> infloc 命令（Pseudo_inf）
    ///   .ini  -> ini 命令（Pseudo_ini）
    ///   .mfl  -> mfl 命令（Pseudo_mfl）
    ///   .psd1 -> psd1 命令（Pseudo_psd1）
    ///   .xml  -> xml 命令（按 //_locTag 拼 XPath）
    ///   .adml -> adml 命令（固定 XPath 列表）
    ///   .resx -> resx 命令（Pseudo_resx）
    /// </summary>
    public static class FileLocalizers
    {
        public static string LocalizeByExtension(string extension, string text)
        {
            if (text == null)
                text = string.Empty;

            string ext = (extension ?? string.Empty).Trim().ToLowerInvariant();
            if (!ext.StartsWith(".", StringComparison.Ordinal))
                ext = "." + ext;

            try
            {
                switch (ext)
                {
                    case ".inf": return LocalizeInf(text);
                    case ".ini": return LocalizeIni(text);
                    case ".mfl": return LocalizeMfl(text);
                    case ".psd1": return LocalizePsd1(text);
                    case ".xml": return LocalizeLocXml(text);
                    case ".adml": return LocalizeAdml(text);
                    case ".resx": return LocalizeResx(text);
                    default: return LocalizePlainText(text);
                }
            }
            catch (System.Xml.XmlException)
            {
                // XML 解析失败就原样返回，避免把文件搞丢
                return text;
            }
        }

        // ==================== 行结构 ====================

        private sealed class LineSet
        {
            public readonly List<string> Items = new List<string>();
            public string NewLine = "\n";

            public string Join()
            {
                return string.Join(NewLine, Items);
            }
        }

        private static LineSet SplitLines(string text)
        {
            var set = new LineSet();
            set.NewLine = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
                set.Items.Add(line);
            return set;
        }

        // ==================== .txt（file 命令）====================

        public static string LocalizePlainText(string text)
        {
            var lines = SplitLines(text);
            for (int i = 0; i < lines.Items.Count; i++)
            {
                // skipempty=true：空行保持空行（原版字节引擎也会原样放过）
                lines.Items[i] = PseudoLocalizer.LocalizeByteCompat(lines.Items[i], true, true, true, true, true);
            }
            return lines.Join();
        }

        // ==================== .inf（infloc 命令）====================

        public static string LocalizeInf(string text)
        {
            var lines = SplitLines(text);
            bool pseudolocalize = false;

            for (int i = 0; i < lines.Items.Count; i++)
            {
                string line = lines.Items[i];

                // 语言 ID 无条件改名
                if (string.Equals(line, "[Strings.0409]", StringComparison.OrdinalIgnoreCase))
                    line = "[Strings.0501]";

                if (line.Length > 0 && line[0] == ';')
                {
                    // 开关完全靠注释里的 "ocalizable" 以及是否含 "on"
                    if (line.IndexOf("ocalizable", StringComparison.Ordinal) >= 0)
                        pseudolocalize = line.IndexOf("on", StringComparison.Ordinal) < 0;
                }
                else if (line.Length > 0 && pseudolocalize)
                {
                    // 只改双引号里的内容
                    string[] parts = line.Split('"');
                    var sb = new StringBuilder();
                    for (int k = 0; k < parts.Length; k++)
                    {
                        sb.Append(k % 2 == 1 ? PseudoLocalizer.LocalizeByteCompat(parts[k]) : parts[k]);
                        if (k != parts.Length - 1)
                            sb.Append('"');
                    }
                    line = sb.ToString();
                }

                lines.Items[i] = line;
            }

            return lines.Join();
        }

        // ==================== .ini（ini 命令）====================

        public static string LocalizeIni(string text)
        {
            var lines = SplitLines(text);
            bool pseudolocalize = false;

            for (int i = 0; i < lines.Items.Count; i++)
            {
                string line = lines.Items[i];

                if (line.IndexOf("009=", StringComparison.Ordinal) >= 0 && !pseudolocalize)
                    line = "101=UNASSIGNED LANGUAGE ID";

                if (line.Length > 0 && line[0] == '[')
                {
                    // 段名含 Objects 或 Text 才处理（区分大小写）
                    pseudolocalize = line.IndexOf("bjects", StringComparison.Ordinal) >= 0 ||
                                     line.IndexOf("ext", StringComparison.Ordinal) >= 0;
                }
                else if (line.Length > 0 && pseudolocalize)
                {
                    line = line.Replace("009", "101");
                    string[] parts = line.Split('=');
                    var sb = new StringBuilder();
                    for (int k = 0; k < parts.Length; k++)
                    {
                        // 原版是拿"已拼好的部分"去找 _000_
                        bool skip = k % 2 == 1 && sb.ToString().IndexOf("_000_", StringComparison.Ordinal) >= 0;
                        sb.Append(skip ? parts[k] : (k % 2 == 1 ? PseudoLocalizer.LocalizeByteCompat(parts[k]) : parts[k]));
                        if (k != parts.Length - 1)
                            sb.Append('=');
                    }
                    line = sb.ToString();
                }

                lines.Items[i] = line;
            }

            return lines.Join();
        }

        // ==================== .mfl（mfl 命令）====================

        public static string LocalizeMfl(string text)
        {
            var lines = SplitLines(text);

            for (int i = 0; i < lines.Items.Count; i++)
            {
                string line = lines.Items[i];

                line = line.Replace("ms_409", "ms_501")
                           .Replace("MS_409", "MS_501")
                           .Replace("0x0409", "0x0501");

                if (line.Length > 0)
                {
                    line = LocalizeFunctionCalls(line, "DisplayName");
                    line = LocalizeFunctionCalls(line, "Description");
                }

                lines.Items[i] = line;
            }

            return lines.Join();
        }

        /// <summary>
        /// 把 Func("...") 括号里的字符串逐个伪本地化，其余部分原样保留。
        /// 反斜杠转义用奇偶个数判断（与 C++ 版一致）。
        /// </summary>
        private static string LocalizeFunctionCalls(string line, string func)
        {
            string key = func + "(\"";
            var builder = new StringBuilder();

            while (true)
            {
                int found = line.IndexOf(key, StringComparison.Ordinal);
                if (found < 0)
                    break;

                builder.Append(line.Substring(0, found + key.Length));
                line = line.Substring(found + key.Length);

                string buffer = null;
                for (int p = 0; p < line.Length; p++)
                {
                    if (line[p] != '"')
                        continue;
                    if (p + 1 >= line.Length || line[p + 1] != ')')
                        continue;

                    if (p == 0)
                    {
                        buffer = string.Empty;
                        line = line.Substring(p + 2);
                        break;
                    }
                    if (line[p - 1] != '\\')
                    {
                        buffer = line.Substring(0, p);
                        line = line.Substring(p + 2);
                        break;
                    }

                    int run = 1;
                    for (; run <= p && line[p - run] == '\\'; run++)
                    {
                    }
                    if (run % 2 == 1)
                    {
                        buffer = line.Substring(0, p);
                        line = line.Substring(p + 2);
                        break;
                    }
                }

                if (buffer == null)
                    break;  // 没找到闭合的 ")，这一处放弃（原版会越界读）

                builder.Append(PseudoLocalizer.LocalizeByteCompat(buffer));
                builder.Append("\")");
            }

            return builder.ToString() + line;
        }

        // ==================== .psd1（psd1 命令）====================

        public static string LocalizePsd1(string text)
        {
            var lines = SplitLines(text);

            for (int i = 0; i < lines.Items.Count; i++)
            {
                string line = lines.Items[i];

                if (line.Length > 0 && line.IndexOf('=') >= 0)
                {
                    string[] parts = line.Split('=');
                    var sb = new StringBuilder();
                    for (int k = 0; k < parts.Length; k++)
                    {
                        bool skip = k % 2 == 1 && sb.ToString().IndexOf("_000_", StringComparison.Ordinal) >= 0;
                        sb.Append(skip ? parts[k] : (k % 2 == 1 ? PseudoLocalizer.LocalizeByteCompat(parts[k]) : parts[k]));
                        if (k != parts.Length - 1)
                            sb.Append('=');
                    }
                    line = sb.ToString();
                }

                lines.Items[i] = line;
            }

            return lines.Join();
        }

        // ==================== XML 公共部分 ====================

        /// <summary>
        /// 对应 MSXML 的 selectNodes，但只支持 "//名字" 和 "//名字/子名字" 这两种形状
        /// （本工具用到的路径全部是这种）。
        ///
        /// 两个原因决定了不能直接用 XPath：
        ///   1. UWP 没有 System.Xml.XPath（XDocument 的 XPath 扩展方法不可用）；
        ///   2. 原版的无前缀 XPath 遇到带默认命名空间的文档（ADML 就是）一个都匹配不到，
        ///      按 local-name 匹配才能让它真的生效。
        /// </summary>
        private static IEnumerable<XElement> SelectElements(XDocument doc, string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("//", StringComparison.Ordinal))
                return new List<XElement>();

            string[] steps = path.Substring(2).Split('/');
            if (steps.Length == 0)
                return new List<XElement>();

            foreach (string step in steps)
            {
                if (!IsSimpleElementName(step))
                    return new List<XElement>();
            }

            IEnumerable<XElement> current = doc.Descendants()
                .Where(e => string.Equals(e.Name.LocalName, steps[0], StringComparison.Ordinal));

            for (int i = 1; i < steps.Length; i++)
            {
                string name = steps[i];
                current = current.SelectMany(e => e.Elements())
                                 .Where(e => string.Equals(e.Name.LocalName, name, StringComparison.Ordinal));
            }

            return current.ToList();
        }

        /// <summary>是否是"纯元素名"（不是带谓词、通配符、命名空间前缀的 XPath 片段）。</summary>
        private static bool IsSimpleElementName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            foreach (char c in name)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                          (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.';
                if (!ok)
                    return false;
            }
            return true;
        }

        private static string Serialize(XDocument doc)
        {
            var sb = new StringBuilder();
            var settings = new System.Xml.XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false,
                NewLineHandling = System.Xml.NewLineHandling.None,   // 别把原文的 \n 改成 \r\n
                CheckCharacters = false
            };
            using (var writer = System.Xml.XmlWriter.Create(sb, settings))
            {
                doc.Save(writer);
            }

            string body = sb.ToString();
            if (doc.Declaration == null)
                return body;

            string decl = doc.Declaration.ToString();
            // 声明和根元素之间原本的换行会被保留在 body 里，别重复加
            if (body.Length > 0 && (body[0] == '\n' || body[0] == '\r'))
                return decl + body;
            return decl + "\n" + body;
        }

        private static void ReplaceText(XElement element, string newText)
        {
            element.ReplaceNodes(new XText(newText));
        }

        // ==================== .xml（xml 命令）====================

        public static string LocalizeLocXml(string text)
        {
            var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);

            // 先从 //_locTag 收集「XPath + 属性名」
            var attrPaths = new List<KeyValuePair<string, string>>();
            foreach (var tag in SelectElements(doc, "//_locTag"))
            {
                string key = (tag.Value ?? string.Empty).Trim();
                string attr = ((string)tag.Attribute("_locAttrData") ?? string.Empty).Trim();
                if (key.Length == 0 || attr.Length == 0)
                    continue;
                if (!key.StartsWith("//", StringComparison.Ordinal))
                    key = "//" + key;
                attrPaths.Add(new KeyValuePair<string, string>(key, attr));
            }

            // 第一遍：文本节点
            foreach (var element in SelectElements(doc, "//String").ToList())
            {
                if (element.HasElements)
                    continue;

                string value = element.Value;
                if (string.IsNullOrEmpty(value))
                    continue;
                ReplaceText(element, PseudoLocalizer.LocalizeXml(value, true, true, true, true, true));
            }

            // 第二遍：属性
            foreach (var pair in attrPaths)
            {
                foreach (var element in SelectElements(doc, pair.Key).ToList())
                {
                    var attr = element.Attribute(pair.Value);
                    if (attr == null)
                        continue;

                    string type = (string)element.Attribute("type") ?? string.Empty;
                    if (string.Equals(type, "string", StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(pair.Value, "value", StringComparison.OrdinalIgnoreCase))
                    {
                        attr.Value = PseudoLocalizer.Localize(attr.Value, true, true, true, false, true);
                    }
                }
            }

            return Serialize(doc);
        }

        // ==================== .adml（adml 命令）====================

        public static string LocalizeAdml(string text)
        {
            var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);

            string[] textPaths =
            {
                "//string", "//dropdownList", "//checkBox", "//listBox", "//label",
                "//decimalTextBox", "//displayName", "//description", "//multiTextBox", "//text"
            };

            foreach (string xpath in textPaths)
            {
                foreach (var element in SelectElements(doc, xpath).ToList())
                {
                    // 只改纯文本节点：像 <checkBox> 这种容器整体替换会吞掉子元素
                    if (element.HasElements)
                        continue;

                    string value = element.Value;
                    if (string.IsNullOrEmpty(value))
                        continue;
                    ReplaceText(element, PseudoLocalizer.LocalizeXml(value, true, true, true, true, true));
                }
            }

            return Serialize(doc);
        }

        // ==================== .resx（resx 命令）====================

        public static string LocalizeResx(string text)
        {
            var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
            var root = doc.Root;
            if (root == null)
                return text;

            foreach (var data in root.Elements().Where(e => e.Name.LocalName == "data").ToList())
            {
                string type = (string)data.Attribute("type") ?? string.Empty;
                string name = (string)data.Attribute("name") ?? string.Empty;

                var valueElement = data.Elements().FirstOrDefault(e => e.Name.LocalName == "value");

                // 值类型（没有 type 属性）且名字像 UI 文本
                bool blocalize = name.IndexOf('.') < 0 && name.IndexOf("_Name", StringComparison.Ordinal) < 0;
                if (!blocalize &&
                    (name.EndsWith(".text", StringComparison.Ordinal) || name.EndsWith(".Text", StringComparison.Ordinal)))
                {
                    blocalize = true;
                }

                if (type.Length == 0 && blocalize && valueElement != null)
                {
                    string value = valueElement.Value;
                    if (!string.IsNullOrEmpty(value) &&
                        !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        ReplaceText(valueElement, PseudoLocalizer.Localize(value, true, true, true, false, true));
                    }
                }

                // 尺寸/坐标放大宽度，给变长的伪本地化文本留位置
                if (valueElement != null &&
                    (string.Equals(type, "System.Drawing.Size, System.Drawing", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(type, "System.Drawing.Point, System.Drawing", StringComparison.OrdinalIgnoreCase)))
                {
                    string value = valueElement.Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        int comma = value.IndexOf(',');
                        string xs = comma < 0 ? value : value.Substring(0, comma);
                        string ys = comma < 0 ? value : value.Substring(comma + 1);
                        int sizeX = (int)(ParseInt(xs) * 1.4);
                        int sizeY = ParseInt(ys);
                        ReplaceText(valueElement, sizeX.ToString() + ", " + sizeY.ToString());
                    }
                }
            }

            return Serialize(doc);
        }

        /// <summary>同 C 的 _wtoi：跳过前导空白，取可选符号 + 连续数字。</summary>
        private static int ParseInt(string s)
        {
            if (string.IsNullOrEmpty(s))
                return 0;

            int i = 0;
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;

            bool negative = false;
            if (i < s.Length && (s[i] == '-' || s[i] == '+'))
            {
                negative = s[i] == '-';
                i++;
            }

            int value = 0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9')
            {
                value = value * 10 + (s[i] - '0');
                i++;
            }

            return negative ? -value : value;
        }
    }

    /// <summary>
    /// 文本文件的解码。原版只区分「UTF-16 LE（有 BOM）」和「其他」两种；
    /// 这里额外把 UTF-8 与"非 UTF-8"分开，预览时中文才不会变成乱码。
    /// </summary>
    public static class TextFile
    {
        public static string Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                // 不是合法 UTF-8：按字节一一对应，至少保证字节不被破坏
                var chars = new char[bytes.Length];
                for (int i = 0; i < bytes.Length; i++)
                    chars[i] = (char)bytes[i];
                return new string(chars);
            }
        }
    }
}
