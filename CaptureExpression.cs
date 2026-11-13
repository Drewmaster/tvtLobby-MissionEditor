using System;
using System.Globalization;
using System.Windows.Forms;

namespace TvtLobby.MissionEditor
{
    // Mirrors the server grammar in tvtLobby_CaptureExpression.c. This is a
    // bounded arithmetic/boolean expression, never C# or Enforce source code.
    public sealed class CaptureExpression
    {
        sealed class Node
        {
            public string Op; public float Value; public bool Boolean; public int Column;
            public Node Left, Right;
            public float Evaluate(int attackers, int defenders)
            {
                if (Op == "number") return Value;
                if (Op == "attackers") return attackers;
                if (Op == "defenders") return defenders;
                float a = Left.Evaluate(attackers, defenders);
                if (Op == "!") return a == 0 ? 1 : 0;
                if (Op == "negate") return -a;
                if (Op == "&&" && a == 0) return 0;
                if (Op == "||" && a != 0) return 1;
                float b = Right.Evaluate(attackers, defenders), value = 0;
                switch (Op)
                {
                    case "&&": case "||": return b != 0 ? 1 : 0;
                    case "==": return a == b ? 1 : 0;
                    case "!=": return a != b ? 1 : 0;
                    case "<": return a < b ? 1 : 0;
                    case "<=": return a <= b ? 1 : 0;
                    case ">": return a > b ? 1 : 0;
                    case ">=": return a >= b ? 1 : 0;
                    case "+": value = a + b; break;
                    case "-": value = a - b; break;
                    case "*": value = a * b; break;
                    case "/":
                        if (b == 0) throw new FormatException("Division by zero at character " + Column);
                        value = a / b; break;
                }
                if (Math.Abs(value) > 1e12f) throw new FormatException("Number too large at character " + Column);
                return value;
            }
        }
        string source, token; int pos, start, kind, nodes; Node root;
        public static CaptureExpression Compile(string source, out string error)
        {
            error = null;
            try
            {
                var expression = new CaptureExpression { source = source ?? "" };
                if (expression.source.Length > 1024) throw new FormatException("Capture expression supports up to 1024 characters.");
                expression.Next(); expression.root = expression.Parse(1, 0);
                if (expression.token != "") expression.Fail("Unexpected '" + expression.token + "'");
                if (!expression.root.Boolean) expression.Fail("Expected a true/false condition; use a comparison such as attackers >= 1");
                return expression;
            }
            catch (FormatException e) { error = e.Message; return null; }
        }
        public bool Evaluate(int attackers, int defenders, out string error)
        {
            error = null;
            try { return root.Evaluate(attackers, defenders) != 0; }
            catch (FormatException e) { error = e.Message; return false; }
        }
        void Fail(string message) { throw new FormatException(message + " (character " + (start + 1) + ")."); }
        static bool Digit(char c) { return c >= '0' && c <= '9'; }
        static bool Letter(char c) { return c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c == '_'; }
        void Next()
        {
            token = ""; kind = 0;
            while (pos < source.Length && " \t\r\n".IndexOf(source[pos]) >= 0) pos++;
            start = pos; if (pos >= source.Length) return;
            char c = source[pos];
            if (Digit(c) || c == '.')
            {
                int digits = 0, dots = 0;
                while (pos < source.Length)
                {
                    c = source[pos]; if (Digit(c)) digits++; else if (c == '.') dots++; else break;
                    pos++;
                }
                token = source.Substring(start, pos - start); kind = 1; float value;
                if (digits == 0 || dots > 1 || !Single.TryParse(token, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) || value > 1000000)
                    Fail("Use a number between 0 and 1000000, with a dot for decimals");
                return;
            }
            if (Letter(c))
            {
                pos++; while (pos < source.Length && (Letter(source[pos]) || Digit(source[pos]))) pos++;
                token = source.Substring(start, pos - start).ToLowerInvariant(); kind = 2;
                if (token == "and") token = "&&"; else if (token == "or") token = "||"; else if (token == "not") token = "!";
                return;
            }
            if (pos + 1 < source.Length)
            {
                string pair = source.Substring(pos, 2);
                if (pair == "&&" || pair == "||" || pair == "==" || pair == "!=" || pair == "<=" || pair == ">=")
                { token = pair; pos += 2; kind = 3; return; }
            }
            token = source[pos++].ToString(); kind = 3;
            if (!"()+-*/!<>".Contains(token)) Fail("Unsupported symbol '" + token + "'");
        }
        static int Priority(string op)
        {
            if (op == "||") return 1;
            if (op == "&&") return 2;
            if (op == "==" || op == "!=" || op == "<" || op == "<=" || op == ">" || op == ">=") return 3;
            if (op == "+" || op == "-") return 4;
            if (op == "*" || op == "/") return 5;
            return 0;
        }
        Node NewNode(string op, bool boolean = false)
        {
            if (++nodes > 256) Fail("Expression is too complex (maximum 256 values/operators)");
            return new Node { Op = op, Boolean = boolean, Column = start + 1 };
        }
        Node Parse(int minimum, int depth)
        {
            if (depth > 32) Fail("Too many nested brackets or operators");
            Node left = Unary(depth);
            while (Priority(token) >= minimum)
            {
                string op = token; int priority = Priority(op); Node node = NewNode(op, priority <= 3);
                Next(); Node right = Parse(priority + 1, depth + 1);
                if (priority <= 2)
                { if (!left.Boolean || !right.Boolean) Fail("'" + op + "' needs true/false conditions on both sides"); }
                else if (op == "==" || op == "!=")
                { if (left.Boolean != right.Boolean) Fail("Compare two numbers or two true/false conditions"); }
                else if (left.Boolean || right.Boolean) Fail("'" + op + "' needs numbers; check your brackets");
                node.Left = left; node.Right = right; left = node;
            }
            return left;
        }
        Node Unary(int depth)
        {
            if (depth > 32) Fail("Too many nested brackets or operators");
            if (token == "!" || token == "-" || token == "+")
            {
                string op = token; Node unary = NewNode(op == "!" ? "!" : "negate", op == "!");
                Next(); unary.Left = Unary(depth + 1);
                if (unary.Boolean != unary.Left.Boolean) Fail("'" + op + "' has the wrong value type; check your brackets");
                return op == "+" ? unary.Left : unary;
            }
            if (token == "(")
            {
                Next(); Node inner = Parse(1, depth + 1);
                if (token != ")") Fail("Missing closing bracket ')'");
                Next(); return inner;
            }
            Node value = null;
            if (kind == 1) { value = NewNode("number"); value.Value = Single.Parse(token, CultureInfo.InvariantCulture); }
            else if (token == "attackers" || token == "a") value = NewNode("attackers");
            else if (token == "defenders" || token == "d") value = NewNode("defenders");
            else if (token == "true" || token == "false") { value = NewNode("number", true); value.Value = token == "true" ? 1 : 0; }
            else Fail("Expected attackers, defenders, a number or '('; found '" + token + "'");
            Next(); return value;
        }
    }

    public sealed partial class ObjectiveForm
    {
        Action capturePreview;
        void ExpressionField(TableLayoutPanel parent, TaskNode owner, TaskField field)
        {
            var card = Card("Capture expression");
            var text = new TextBox { Text = field.Value ?? "", Multiline = true, Height = 62, MaxLength = 1024, ScrollBars = ScrollBars.Vertical,
                Name = "Objective_" + owner.TypeName + "_CaptureExpression", AccessibleName = "Capture expression" };
            EditorTheme.Add(card, text);
            EditorTheme.Add(card, EditorTheme.Label("attackers (a): eligible participants of the current capturing subject in the zone.\ndefenders (d): opposing participants in the zone, using Opposing presence from Setup. If empty, everyone outside the capturing subject counts.", 9, EditorTheme.Muted));
            EditorTheme.Add(card, EditorTheme.Label("Brackets ( ) · arithmetic + − * / · comparisons < <= > >= == !=\nAND && · OR || · NOT !   (and / or / not also work)", 9, EditorTheme.Muted));
            EditorTheme.Add(card, EditorTheme.Label("Try sample counts", 10, null, true));
            var attackers = new NumericUpDown { Minimum = 0, Maximum = 1000000, Value = 9, Name = "CaptureSampleAttackers" };
            var defenders = new NumericUpDown { Minimum = 0, Maximum = 1000000, Value = 3, Name = "CaptureSampleDefenders" };
            EditorTheme.Add(card, EditorTheme.Pair(EditorTheme.Field("Attackers", attackers), EditorTheme.Field("Defenders", defenders)));
            var result = EditorTheme.Label("", 9, EditorTheme.Amber); result.Name = "CaptureExpressionResult";
            EditorTheme.Add(card, result);
            EditorTheme.Add(card, EditorTheme.Label("Sample counts do not change the mission. Minimum capturing participants still applies. The condition must hold for the capture duration below; Keep progress controls interruptions.", 9, EditorTheme.Muted));
            Action preview = () =>
            {
                string error; var parsed = CaptureExpression.Compile(text.Text, out error);
                if (parsed == null) { result.Text = error; return; }
                bool met = parsed.Evaluate((int)attackers.Value, (int)defenders.Value, out error);
                int minimum; if (!Int32.TryParse(owner.Text("MinimumPresence"), out minimum)) minimum = 1;
                result.Text = error != null ? error + " — condition not met." : attackers.Value < minimum ? "Condition not met: requires at least " + minimum + " capturing participants." : met ? "Condition met — capture timer can advance." : "Condition not met — capture timer cannot advance.";
            };
            text.TextChanged += (s, e) => { field.Value = text.Text; preview(); Touch(); };
            attackers.ValueChanged += (s, e) => preview(); defenders.ValueChanged += (s, e) => preview();
            // Numeric fields outside this card may change the base presence gate.
            capturePreview = preview;
            preview(); EditorTheme.Add(parent, card);
        }
    }
}
