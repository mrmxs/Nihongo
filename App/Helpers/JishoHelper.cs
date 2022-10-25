using System.Text.RegularExpressions;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using Nihongo.Core.Models;

namespace Nihongo.App.Helpers;

public class JishoHelper
{
    // Basic Kanji Information
    // https://jisho.org/search/劇获画%23kanji
    // todo Strokes order
    // http://kanjivg.tagaini.net
    // https://d1w6u4xc3l95km.cloudfront.net/kanji-2015-03/09b3c.svg

    private HtmlNode _htmlNode;
    private Dictionary<string, string> _svgSrc;

    public JishoHelper(string htmlSrc)
    {
        _htmlNode = DocNode(htmlSrc);
        _svgSrc = SvgSrcParser();

        var kanjiDetails = _htmlNode.QuerySelectorAll(".kanji.details");

        Kanjis = kanjiDetails
            .Select(kd =>
            {
                var k = kd.QuerySelector(".character").InnerHtml;
                var m = kd.QuerySelector(".kanji-details__main-meanings").InnerText.Trim();
                var kun = kd.QuerySelectorAll(".kun_yomi .kanji-details__main-readings-list a")?
                    .Select(r => r.InnerHtml).ToArray();
                var on = kd.QuerySelectorAll(".on_yomi .kanji-details__main-readings-list a")?
                    .Select(r => r.InnerHtml).ToArray();
                var g = kd.QuerySelector(".grade strong")?.InnerHtml
                    .Replace("grade ", "")
                    .Replace("junior high", "H");
                var j = kd.QuerySelector(".jlpt strong")?.InnerHtml;
                var f = kd.QuerySelector(".frequency strong")?.InnerHtml;
                var s = StrokeOrderDiagramGenerator(
                    container: kd.QuerySelector(".stroke_order_diagram--outer_container"));

                return new JishoKanji
                {
                    Kanji = k,
                    Meaning = m,
                    KunReading = kun,
                    OnReading = on,
                    Grade = g,
                    JLPT = j,
                    Frequency = f,
                    StrokeOrder = s
                };
            }).ToList();
    }

    public IEnumerable<JishoKanji> Kanjis { get; internal set; }

    private HtmlNode DocNode(string src)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(src);

        return doc.DocumentNode;
    }

    private Dictionary<string, string> SvgSrcParser()
    {
        // var url = '//d1w6u4xc3l95km.cloudfront.net/kanji-2015-03/09b3c.svg';
        // var el = $('#kanji_strokes_51866279d5dda796580001ea');

        var pattern = @"'(//.+cloudfront[^']+)'[^']+'(#[^']+)'";
        var matches = Regex.Matches(_htmlNode.InnerHtml, pattern, RegexOptions.None);

        return
            matches?.ToDictionary(
                keySelector: el =>
                    el.Groups[2].Value.Replace("#kanji_strokes_", ""),
                elementSelector: el =>
                    $"https:{el.Groups[1].Value}")

            ?? new Dictionary<string, string>();
    }

    private string StrokeOrderDiagramGenerator(HtmlNode container)
    {
        // <svg class="stroke_order_diagram--svg_container_for_51866279d5dda796580001ea"

        var kanjiId = container.Element("svg")
            .Attributes.First(a => a.Name == "class").Value
            .Replace("stroke_order_diagram--svg_container_for_", "");

        var kanjivgPath = _svgSrc[kanjiId];
        var kanjivgSrc = GetHelper.FromUrl(kanjivgPath);
        var kanjivg = DocNode(kanjivgSrc);

        return SVGToDiagramConverter(kanjivg).OuterHtml;
    }

    private HtmlNode SVGToDiagramConverter(HtmlNode kanjivg) //mb xml, mb svg
    {
        var diagram = HtmlNode.CreateNode("<svg></svg>");

        var id = Regex.Match( // id="kvg:StrokeNumbers_058eb"
                kanjivg.QuerySelector("[id*='StrokeNumbers']").Id,
                "[^_]+_(.+)",
                RegexOptions.None
            ).Groups[1].Value;
        var strokes = int.Parse(
            kanjivg.QuerySelector("[id*='StrokeNumbers'] :last-child").InnerText);

        for (int i = 0; i <= strokes; i++)
        {
            if (0 == i) 
            {
                diagram.AppendChild(HtmlNode.CreateNode(
                    $@"<g id='{id}_borders'>
    <line x1='1' x2='{strokes * 100 - 1}' y1='1'  y2='1'  class='stroke_order_diagram--bounding_box'></line>
    <line x1='1' x2='1'                   y1='1'  y2='99' class='stroke_order_diagram--bounding_box'></line>
    <line x1='1' x2='{strokes * 100 - 1}' y1='99' y2='99' class='stroke_order_diagram--bounding_box'></line>
    <line x1='0' x2='{strokes * 100}'     y1='50' y2='50' class='stroke_order_diagram--guide_line'></line>
</g>".Replace("\\n", "")));
            }

            var strokeNode = HtmlNode.CreateNode($@"<g id='{id}_{i}'>
    <line x1='{(i - 1) + 50}' x2='{(i - 1) + 50}' y1='1' y2='99' class='stroke_order_diagram--guide_line'></line>
    <line x1='{i * 100 - 1}'  x2='{i * 100 - 1}'  y1='1' y2='99' class='stroke_order_diagram--bounding_box'></line>
</g>");
            // todo transform, del "\n"

            // todo paths

            //todo circles

            diagram.AppendChild(strokeNode);
        }

        return diagram;
    }
}