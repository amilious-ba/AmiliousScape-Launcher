using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using HtmlAgilityPack;

namespace Saradomin.Utilities
{
    public class HtmlRenderer
    {
        private InlineCollection _inlines;
        private readonly HtmlNode _rootNode;

        public HtmlRenderer(HtmlNode rootNode)
        {
            _rootNode = rootNode;
        }

        public InlineCollection Render()
        {
            _inlines = new InlineCollection();
            Visit(_rootNode);
            return _inlines;
        }

        private void AddRun(string text, FontWeight weight = FontWeight.Normal, double fontSize = 13, FontStyle style = FontStyle.Normal)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Collapse runs of whitespace but keep single spaces
            text = Regex.Replace(text, @"[ \t]+", " ");

            _inlines.Add(new Run(text)
            {
                FontWeight = weight,
                FontSize = fontSize,
                FontStyle = style
            });
        }

        private void Visit(HtmlNode node)
        {
            switch (node.Name.ToLowerInvariant())
            {
                case "#text":
                    AddRun(HttpUtility.HtmlDecode(node.InnerText));
                    break;

                case "br":
                    _inlines.Add(new LineBreak());
                    break;

                case "p":
                    foreach (var child in node.ChildNodes)
                        Visit(child);
                    _inlines.Add(new LineBreak());          // only one break after paragraph
                    break;

                case "h1":
                case "h2":
                case "h3":
                case "h4":
                    _inlines.Add(new LineBreak());
                    var size = node.Name switch
                    {
                        "h1" => 17.0,
                        "h2" => 15.5,
                        "h3" => 14.0,
                        _    => 13.5
                    };
                    foreach (var child in node.ChildNodes)
                        VisitStyled(child, FontWeight.Bold, size);
                    _inlines.Add(new LineBreak());
                    break;

                case "b":
                case "strong":
                    foreach (var child in node.ChildNodes)
                        VisitStyled(child, FontWeight.Bold);
                    break;

                case "i":
                case "em":
                    foreach (var child in node.ChildNodes)
                        VisitStyled(child, FontWeight.Normal, 13, FontStyle.Italic);
                    break;

                case "code":
                    foreach (var child in node.ChildNodes)
                        VisitStyled(child, FontWeight.SemiBold, 12.5);
                    break;

                case "pre":
                    var preText = HttpUtility.HtmlDecode(node.InnerText);
                    foreach (var line in preText.Replace("\r\n", "\n").Split('\n'))
                    {
                        AddRun(line, FontWeight.Normal, 12);
                        _inlines.Add(new LineBreak());
                    }
                    break;

                case "ul":
                case "ol":
                    foreach (var child in node.ChildNodes)
                        Visit(child);
                    break;

                case "li":
                    _inlines.Add(new Run("• "));
                    foreach (var child in node.ChildNodes)
                        Visit(child);
                    _inlines.Add(new LineBreak());
                    break;

                case "a":
                    foreach (var child in node.ChildNodes)
                        Visit(child);
                    break;
                case "table":
                    _inlines.Add(new LineBreak());
                    foreach (var row in node.SelectNodes(".//tr") ?? Enumerable.Empty<HtmlNode>()) {
                        var cells = row.SelectNodes("./th|./td");
                        if (cells == null || cells.Count == 0) continue;

                        // First cell = key, rest = value
                        var key = HttpUtility.HtmlDecode(cells[0].InnerText).Trim();
                        var value = cells.Count > 1
                            ? HttpUtility.HtmlDecode(string.Join(" ", cells.Skip(1).Select(c => c.InnerText))).Trim()
                            : "";

                        if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(value))
                            continue;

                        // Skip pure separator rows (---|---)
                        if (key.All(c => c == '-' || c == '|' || c == ' '))
                            continue;

                        // Bold key
                        _inlines.Add(new Run("  " + key)
                        {
                            FontWeight = FontWeight.SemiBold,
                            FontSize = 13
                        });

                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            _inlines.Add(new Run(" → ") { FontSize = 13 });
                            _inlines.Add(new Run(value) { FontSize = 13 });
                        }

                        _inlines.Add(new LineBreak());
                    }
                    _inlines.Add(new LineBreak());
                    break;
                default:
                    foreach (var child in node.ChildNodes)
                        Visit(child);
                    break;
            }
        }

        private void VisitStyled(HtmlNode node, FontWeight weight, double fontSize = 13, FontStyle style = FontStyle.Normal)
        {
            if (node.Name == "#text")
            {
                AddRun(HttpUtility.HtmlDecode(node.InnerText), weight, fontSize, style);
            }
            else
            {
                Visit(node); // nested tags – approximate
            }
        }
        
        private static Run MakeLine(int length = 48)
        {
            return new Run(new string('─', length))   // or '─', '━', '═', '-'
            {
                FontSize = 11,
                Foreground = Brushes.Gray          // optional
            };
        }
        
    }
}