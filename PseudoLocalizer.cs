using System;
using System.Text;

namespace PseudolocalizeUWP
{
    /// <summary>
    /// 伪本地化引擎。
    ///
    /// 移植自 C++ 版 pseudolocalize 的 Pseudo_localize_utf8()
    /// （pseudo_utf8.cpp 第 76~238 行），行为逐条对齐：
    ///   1. 生成 5 位随机 ID 作为本次字符串的指纹
    ///   2. 前缀过滤器（XML 声明 / 0x 十六进制 / Normal... / Segoe 字体名）
    ///   3. 逐字符替换成形近的拉丁扩展 / 希腊 / 西里尔字母
    ///   4. 保护 %格式串、\转义、&lt;标签&gt;、&amp; 实体、_下划线
    ///   5. 跳过宏名、纯符号串；按原长度 30% 追加 " !!!" 填充
    ///   6. 套上 [ ]，前面贴 [ID]
    ///
    /// 说明：命令行版 string 命令用的是字节模式引擎 Pseudo_localize()，
    /// 它把 UTF-8 字节序列塞进宽字符里，靠 C locale 的流窄化还原。
    /// 那是为字节流文件服务的技巧，在 UI 文本框里只会得到双重编码的乱码，
    /// 所以这里用的是同一套字符表的 Unicode 版引擎——所有文件格式命令
    /// 用的也正是这一版。
    /// </summary>
    public static class PseudoLocalizer
    {
        private const string Charset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        private static readonly Random Rng = new Random();
        private static readonly object Gate = new object();

        /// <summary>随机数来源，可替换以便做确定性测试（对应 C 的 random(x) = rand() % x）。</summary>
        internal static Func<int, int> RandomInt = n =>
        {
            lock (Gate)
            {
                return Rng.Next(n);
            }
        };

        // 索引 0..25 = 'A'..'Z'，26..51 = 'a'..'z'。
        // 每项是该字母的形近字符候选表，随机取一个。
        private static readonly string[] Map =
        {
            "ÀÁÂÃÄÅĀĂĄǺΆΑΔΛАД",   // A (16)
            "ßΒβБВЪЬ",   // B (7)
            "ÇĆĈĊČС€",   // C (7)
            "ÐĎĐ",   // D (3)
            "ÈÉÊËΈΕΣЁЄЕЗЭ∑ĒĖĘĚĔ",   // E (18)
            "₣",   // F (1)
            "ĜĠĢĞ",   // G (4)
            "ΉΗНĤĦ",   // H (5)
            "ÌÍÎΊΪІЇĨĪĬĮİ",   // I (12)
            "ЈĴ",   // J (2)
            "ΚЌКĶ",   // K (4)
            "£₤ĹĻĽĿŁ",   // L (7)
            "ΜМ",   // M (2)
            "ÑΝИЙЛП∏ŃŇŅŊ",   // N (11)
            "ÒÓÔÕÖØǾΌΏΘΟΩОФΩŌŎŐ",   // O (18)
            "ÞΡР",   // P (3)
            "Q",   // Q (1)
            "ΓЃГЯҐŔŖŘ",   // R (8)
            "ЅŚŜŞŠ",   // S (5)
            "ΤТŢŤŦ",   // T (5)
            "ÙÚÛÜЏЦŨŪŬŮŰŲ",   // U (12)
            "V",   // V (1)
            "ШẀẂẄŴ",   // W (5)
            "ΧχЖХ",   // X (4)
            "¥ÝΎΥΫЎỲŶ",   // Y (8)
            "ΖŹŻŽ",   // Z (4)
            "ªàáâãäåăāąǻάαад∆",   // a (16)
            "въь",   // b (3)
            "¢çćĉċčςс",   // c (8)
            "ðδđď",   // d (4)
            "èéêëέεезэёє℮ĕēėęě",   // e (17)
            "ƒ",   // f (1)
            "ğĝġģ",   // g (4)
            "нђћħĥ",   // h (5)
            "ìíîïΐίιϊіїīĩĭį",   // i (14)
            "јĵ",   // j (2)
            "κкќķĸ",   // k (5)
            "ℓļĺľŀł",   // l (6)
            "мɱḿ",   // m (3)
            "ñήηπийлпⁿņňŉŋń",   // n (14)
            "òóôõöøǿΦθοσόбоōŏő",   // o (17)
            "ρφр",   // p (3)
            "q",   // q (1)
            "гяѓґřŕŗ",   // r (7)
            "ѕšśşŝ",   // s (5)
            "τтŧţť",   // t (5)
            "µùúûüΰμυϋύцџųũűůŭū",   // u (18)
            "νʋ",   // v (2)
            "ωώшщẅẁẃŵ",   // w (8)
            "×жх",   // x (3)
            "ýÿγуўỳŷ",   // y (7)
            "žźż",   // z (3)
        };

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        private static string Pick(char c)
        {
            string candidates;
            if (c >= 'a' && c <= 'z')
                candidates = Map[26 + (c - 'a')];
            else if (c >= 'A' && c <= 'Z')
                candidates = Map[c - 'A'];
            else
                return c.ToString();

            return candidates[RandomInt(candidates.Length)].ToString();
        }

        public static string Localize(string str,
                                      bool genid = true,
                                      bool wraparound = true,
                                      bool extend = true,
                                      bool genidwithnewline = true,
                                      bool skipempty = false)
        {
            return LocalizeCore(str, false, genid, wraparound, extend, genidwithnewline, skipempty);
        }

        /// <summary>
        /// 对应 C++ 的 Pseudo_localize()（字节模式引擎）。与 Unicode 版只差跳过规则：
        /// 只要没有任何小写字母就整体放行（宏名、全大写常量通常不该动）。
        /// 文本格式（inf/ini/mfl/psd1/txt）的非 UTF-16 分支走的正是这一版。
        /// </summary>
        public static string LocalizeByteCompat(string str,
                                                bool genid = true,
                                                bool wraparound = true,
                                                bool extend = true,
                                                bool genidwithnewline = true,
                                                bool skipempty = false)
        {
            return LocalizeCore(str, true, genid, wraparound, extend, genidwithnewline, skipempty);
        }

        private static string LocalizeCore(string str,
                                           bool byteRules,
                                           bool genid,
                                           bool wraparound,
                                           bool extend,
                                           bool genidwithnewline,
                                           bool skipempty)
        {
            if (str == null)
                str = string.Empty;

            // ---- 1. 生成 5 位随机 ID ----
            var buffer = new char[5];
            for (int k = 0; k < 5; k++)
                buffer[k] = Charset[RandomInt(Charset.Length)];
            string id = new string(buffer);

            // ---- 2. 前缀过滤器：命中就原样返回 ----
            if (str.Length > 10 && str[0] == '<' && str[1] == '?' && str[2] == 'x' && str[3] == 'm' && str[4] == 'l')
                return str;                                     // <?xml 声明
            if (str.Length > 3 && str[0] == '0' && (str[1] == 'x' || str[1] == 'X'))
                return str;                                     // 0x8000xxxx 之类的十六进制
            if (str.Length >= 6 && str[0] == 'N' && str[1] == 'o' && str[2] == 'r' &&
                str[3] == 'm' && str[4] == 'a' && str[5] == 'l')
                return str;                                     // Normal color / Normal size
            if (str.IndexOf("Segoe", StringComparison.Ordinal) >= 0 ||
                str.IndexOf("segoe", StringComparison.Ordinal) >= 0)
                return str;                                     // 字体名

            // ---- 3. 逐字符替换 ----
            var sb = new StringBuilder(str.Length * 2);
            int len = str.Length;
            bool nolower = true;        // 没有任何小写字母
            bool noupper = true;        // 没有任何大写字母
            bool hasunderline = false;  // 含下划线

            for (int i = 0; i < len; i++)
            {
                char c = str[i];
                string outStr;

                if (c >= 'a' && c <= 'z')
                {
                    outStr = Pick(c);
                    nolower = false;
                }
                else if (c >= 'A' && c <= 'Z')
                {
                    outStr = Pick(c);
                    noupper = false;
                }
                else
                {
                    switch (c)
                    {
                        case '\n':
                            outStr = (genid && genidwithnewline) ? "\n[" + id + "]" : "\n";
                            break;

                        case '%':
                            // 保护 %1!s!、%ls、%d、%W...% 这类格式串
                            outStr = string.Empty;
                            while (i < len)
                            {
                                outStr += str[i];
                                char ch = str[i];
                                if (IsAsciiLetter(ch) && ch != 'l' && ch != 'W' && ch != 'w')
                                    break;
                                if (ch == 'W')
                                {
                                    for (i++; i < len; i++)
                                    {
                                        outStr += str[i];
                                        if (str[i] == '%')
                                            break;
                                    }
                                    break;
                                }
                                i++;
                            }
                            break;

                        case '\\':
                            // 保护 \n、\t、\x41、\u1234 这类转义
                            outStr = string.Empty;
                            while (i < len)
                            {
                                outStr += str[i];
                                if (IsAsciiLetter(str[i]))
                                    break;
                                i++;
                            }
                            break;

                        case '<':
                            // 保护 XML/HTML 标签
                            outStr = string.Empty;
                            while (i < len)
                            {
                                outStr += str[i];
                                if (str[i] == '>')
                                    break;
                                i++;
                            }
                            break;

                        case '&':
                            // 保护 &amp; 以及 &File 这种加速键标记
                            outStr = string.Empty;
                            while (i < len)
                            {
                                outStr += str[i];
                                if (IsAsciiLetter(str[i]))
                                    break;
                                i++;
                            }
                            break;

                        case '_':
                            hasunderline = true;
                            outStr = "_";
                            break;

                        default:
                            outStr = c.ToString();
                            break;
                    }
                }

                // 文本里若是字面的 \n（反斜杠 + n），补上 ID 以便定位换行
                if (genid && genidwithnewline && string.Equals(outStr, "\\n", StringComparison.OrdinalIgnoreCase))
                    outStr = "]\\n[" + id + "][";

                sb.Append(outStr);
            }

            string after = sb.ToString();

            // ---- 4. 判定 ----
            bool isempty = true;
            for (int i = 0; i < after.Length; i++)
            {
                char ch = after[i];
                if (ch != ' ' && ch != '\t' && ch != '\0' && ch != '\n' && ch != '\r')
                {
                    isempty = false;
                    break;
                }
            }
            isempty = isempty || after.Length == 0;

            if (isempty && skipempty)
                return str;

            if (byteRules)
            {
                // 字节模式：没有小写字母就整体放过
                if (nolower)
                    return str;
            }
            else
            {
                if (nolower && hasunderline)
                    return str;                                 // 看着像宏/资源 ID，不动
                if (nolower && noupper && !isempty)
                    return str;                                 // 没有任何字母，不动
            }

            // ---- 5. 长度填充：按原长度追加约 30% ----
            if (extend)
            {
                int lencnt = (int)Math.Floor(len * 3.0 / 10.0 + 0.5);
                for (int k = 1; k <= lencnt; k++)
                    sb.Append(k % 4 == 1 ? ' ' : '!');
            }

            // ---- 6. 包装 ----
            string body = sb.ToString();
            string tag = "[" + id + "]";
            if (wraparound)
                body = "[" + body + "]";
            if (genid)
                body = tag + body;

            return body;
        }

        /// <summary>
        /// 对应 C++ 的 Pseudo_localize_utf8_xml()：专给 XML 文本节点用。
        /// 与 Localize 的差别只有四处：
        ///   1. 先把 &amp;lt; / &amp;gt; 还原成 &lt; / &gt;；
        ///   2. 没有反斜杠转义分支（反斜杠按普通字符处理）；
        ///   3. 不用"没有任何大写字母"这条跳过规则；
        ///   4. 填充长度按实际处理过的字符数（reallen）算，而不是原串长度。
        /// </summary>
        public static string LocalizeXml(string str,
                                         bool genid = true,
                                         bool wraparound = true,
                                         bool extend = true,
                                         bool genidwithnewline = true,
                                         bool skipempty = false)
        {
            if (str == null)
                str = string.Empty;

            str = str.Replace("&lt;", "<").Replace("&gt;", ">");

            var buffer = new char[5];
            for (int k = 0; k < 5; k++)
                buffer[k] = Charset[RandomInt(Charset.Length)];
            string id = new string(buffer);

            if (str.Length > 10 && str[0] == '<' && str[1] == '?' && str[2] == 'x' && str[3] == 'm' && str[4] == 'l')
                return str;

            var sb = new StringBuilder(str.Length * 2);
            int len = str.Length;
            int reallen = 0;
            bool nolower = true;
            bool hasunderline = false;

            for (int i = 0; i < len; i++)
            {
                char c = str[i];
                reallen++;
                string outStr;

                if (c >= 'a' && c <= 'z')
                {
                    outStr = Pick(c);
                    nolower = false;
                }
                else if (c >= 'A' && c <= 'Z')
                {
                    outStr = Pick(c);
                }
                else
                {
                    switch (c)
                    {
                        case '\n':
                            outStr = (genid && genidwithnewline) ? "\n[" + id + "]" : "\n";
                            break;

                        case '%':
                            outStr = string.Empty;
                            while (i < len)
                            {
                                outStr += str[i];
                                char ch = str[i];
                                if (IsAsciiLetter(ch) && ch != 'l' && ch != 'W')
                                    break;
                                if (ch == 'W')
                                {
                                    for (i++; i < len; i++)
                                    {
                                        outStr += str[i];
                                        if (str[i] == '%')
                                            break;
                                    }
                                    break;
                                }
                                i++;
                            }
                            break;

                        case '<':
                            outStr = string.Empty;
                            while (i < len)
                            {
                                outStr += str[i];
                                if (str[i] == '>')
                                    break;
                                i++;
                            }
                            break;

                        case '&':
                            outStr = string.Empty;
                            while (i < len)
                            {
                                outStr += str[i];
                                if (IsAsciiLetter(str[i]))
                                    break;
                                i++;
                            }
                            break;

                        case '_':
                            hasunderline = true;
                            outStr = "_";
                            break;

                        default:
                            outStr = c.ToString();
                            break;
                    }
                }

                sb.Append(outStr);
            }

            string after = sb.ToString();

            bool isempty = true;
            for (int i = 0; i < after.Length; i++)
            {
                char ch = after[i];
                if (ch != ' ' && ch != '\t' && ch != '\0' && ch != '\n' && ch != '\r')
                {
                    isempty = false;
                    break;
                }
            }
            isempty = isempty || after.Length == 0;

            if (isempty && skipempty)
                return str;
            if (nolower && hasunderline)
                return str;

            if (extend)
            {
                int lencnt = (int)Math.Floor(reallen * 3.0 / 10.0 + 0.5);
                for (int k = 1; k <= lencnt; k++)
                    sb.Append(k % 4 == 1 ? ' ' : '!');
            }

            string body = sb.ToString();
            string tag = "[" + id + "]";
            if (wraparound)
                body = "[" + body + "]";
            if (genid)
                body = tag + body;

            return body;
        }
    }
}
