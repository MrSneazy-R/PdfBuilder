using System.Threading;
using PdfBuilder.Models;

namespace PdfBuilder.Document.Layout;

internal sealed class TableLayoutDiagnosticsSession
{
    private int _enabled;
    private long _tableMeasurementCount;
    private long _tableRowMeasurementCount;
    private long _tableCellMeasurementCount;
    private long _tableCloneCount;
    private long _tableRowCloneCount;
    private long _contentFactoryInvocationCount;
    private long _cellDrawBufferAllocationCount;

    public bool Enabled
    {
        get => Volatile.Read(ref _enabled) != 0;
        set => Volatile.Write(ref _enabled, value ? 1 : 0);
    }

    public void RecordTableMeasurement() { if (Enabled) Interlocked.Increment(ref _tableMeasurementCount); }
    public void RecordTableRowMeasurement() { if (Enabled) Interlocked.Increment(ref _tableRowMeasurementCount); }
    public void RecordTableCellMeasurement() { if (Enabled) Interlocked.Increment(ref _tableCellMeasurementCount); }
    public void RecordTableClone() { if (Enabled) Interlocked.Increment(ref _tableCloneCount); }
    public void RecordTableRowClone() { if (Enabled) Interlocked.Increment(ref _tableRowCloneCount); }
    public void RecordContentFactoryInvocation() { if (Enabled) Interlocked.Increment(ref _contentFactoryInvocationCount); }
    public void RecordCellDrawBufferAllocation() { if (Enabled) Interlocked.Increment(ref _cellDrawBufferAllocationCount); }

    public void CopyTo(PdfGenerationMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        metrics.TableMeasurementCount = Interlocked.Read(ref _tableMeasurementCount);
        metrics.TableRowMeasurementCount = Interlocked.Read(ref _tableRowMeasurementCount);
        metrics.TableCellMeasurementCount = Interlocked.Read(ref _tableCellMeasurementCount);
        metrics.TableCloneCount = Interlocked.Read(ref _tableCloneCount);
        metrics.TableRowCloneCount = Interlocked.Read(ref _tableRowCloneCount);
        metrics.ContentFactoryInvocationCount = Interlocked.Read(ref _contentFactoryInvocationCount);
        metrics.TableCellDrawBufferAllocationCount = Interlocked.Read(ref _cellDrawBufferAllocationCount);
    }
}
