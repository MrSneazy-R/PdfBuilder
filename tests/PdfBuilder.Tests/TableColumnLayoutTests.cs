using PdfBuilder.Document;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using PdfBuilder.Elements;
using PdfBuilder.Elements.Table;
using Xunit;

namespace PdfBuilder.Tests
{
    public class TableColumnLayoutTests
    {
        [Fact]
        public void ColumnDefinitions_MixFixedAndRelative_RespectsWeights()
        {
            var table = new TableElement(0, 0)
            {
                TableWidth = 360
            };

            table.ColumnDefinitions = new List<TableColumnDefinition>
            {
                TableColumn.Fixed(120),
                TableColumn.Relative(1f, minWidth: 60f),
                TableColumn.Relative(2f, minWidth: 60f, maxWidth: 240f)
            };

            table.Rows.Add(new TableRow { Cells = { new TableCell(), new TableCell(), new TableCell() } });

            var widths = InvokeCalculator(table, 3, 360);

            widths.Should().HaveCount(3);
            widths[0].Should().BeApproximately(120, 0.01f);
            widths.Sum().Should().BeApproximately(360, 0.01f);
            widths[2].Should().BeGreaterThan(widths[1]);
            widths[1].Should().BeGreaterThanOrEqualTo(60);
        }

        [Fact]
        public void ColumnDefinitions_MinAndMax_AreHonoured()
        {
            var table = new TableElement(0, 0)
            {
                TableWidth = 300
            };

            table.ColumnDefinitions = new List<TableColumnDefinition>
            {
                TableColumn.Auto(minWidth: 100f, maxWidth: 120f),
                TableColumn.Relative(1f, minWidth: 80f, maxWidth: 120f),
                TableColumn.Relative(1f, minWidth: 80f, maxWidth: 120f)
            };

            table.Rows.Add(new TableRow { Cells = { new TableCell { Text = "AAAA" }, new TableCell { Text = "BBBB" }, new TableCell { Text = "CCCC" } } });

            var widths = InvokeCalculator(table, 3, 300);

            widths.All(w => w >= 80f).Should().BeTrue();
            widths.All(w => w <= 120f + 0.1f).Should().BeTrue();
            widths.Sum().Should().BeApproximately(300, 0.01f);
        }

        private static float[] InvokeCalculator(TableElement table, int columns, float width)
        {
            var calculatorType = typeof(TableBuilder).Assembly.GetType("PdfBuilder.Document.Layout.TableColumnWidthCalculator");
            var method = calculatorType!.GetMethod("Calculate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return (float[])method!.Invoke(null, new object[] { table, columns, width })!;
        }
    }
}

