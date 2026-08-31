using System.Data;
using System.Text;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Vectors;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.BatchMode;
using InternalsViewer.Execution.Iterators.BatchMode;
using InternalsViewer.Execution.Iterators.Common;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Tests.UnitTests.BatchMode;

[Trait("Category", "Unit")]
[Trait("Area", "BatchMode")]
public class RowToBatchIteratorTests
{
    [Fact]
    public async Task Rows_Pack_Into_One_Batch_Until_The_Input_Is_Exhausted()
    {
        var batch = await PackAsync(Rows(10));

        Assert.NotNull(batch);

        Assert.Equal(10, batch.RowCount);

        Assert.Equal(10, batch.SelectionVector.RowCount);

        Assert.Equal(2, batch.Vectors.Count);
    }

    [Fact]
    public async Task A_Full_Batch_Stops_At_The_Capacity()
    {
        var iterator = Create(Rows(BatchSize.MaxRowCount + 5));

        var first = await iterator.GetNextBatchAsync(CancellationToken.None);

        Assert.NotNull(first);

        Assert.Equal(first.Capacity, first.RowCount);

        var second = await iterator.GetNextBatchAsync(CancellationToken.None);

        Assert.NotNull(second);

        Assert.Equal(BatchSize.MaxRowCount + 5 - first.Capacity, second.RowCount);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task An_Empty_Input_Produces_No_Batch()
    {
        Assert.Null(await PackAsync([]));
    }

    [Theory]
    [InlineData(SqlDbType.BigInt, 4611686018427387L)]
    [InlineData(SqlDbType.Int, 1900000L)]
    [InlineData(SqlDbType.Bit, 1L)]
    public async Task An_Integer_Round_Trips_Through_The_Vector(SqlDbType dataType, long value)
    {
        var record = Record(new ComputedField("Value", AccessValue.FromInteger(dataType, value)));

        var read = await MaterialiseAsync(record);

        Assert.Equal(value, read.Numeric);
    }

    [Fact]
    public async Task A_Real_Round_Trips_Through_The_Vector()
    {
        var record = Record(new ComputedField("Value", AccessValue.FromReal(SqlDbType.Float, -1234.5678)));

        var read = await MaterialiseAsync(record);

        Assert.Equal(-1234.5678, read.Real);
    }

    [Fact]
    public async Task A_Null_Round_Trips_As_Null()
    {
        var record = Record(new ComputedField("Value", AccessValue.FromNull(SqlDbType.BigInt)));

        Assert.True((await MaterialiseAsync(record)).IsNull);
    }

    [Fact]
    public async Task A_String_Round_Trips_Through_Deep_Data()
    {
        var text = Encoding.Unicode.GetBytes("North");

        var record = Record(new ComputedField("Value", AccessValue.FromBytes(SqlDbType.NVarChar, text)));

        var read = await MaterialiseAsync(record);

        Assert.Equal<byte>(text, read.Data.ToArray());
    }

    private static async Task<AccessValue> MaterialiseAsync(IRecord record)
    {
        var batch = await PackAsync([record]);

        Assert.NotNull(batch);

        var row = BatchRecordBuilder.Build(batch, batch.SelectionVector[0]);

        return ((ComputedField)row.Fields[0]).ComputedValue;
    }

    private static async Task<ExecutionBatch?> PackAsync(IReadOnlyList<IRecord> rows)
        => await Create(rows).GetNextBatchAsync(CancellationToken.None);

    private static RowToBatchIterator Create(IReadOnlyList<IRecord> rows)
    {
        var source = new StubIterator(rows);

        var iterator = new RowToBatchIterator(new StubFactory(source));

        iterator.OpenAsync(new RowToBatchDefinition(new SelectDefinition(new AllocationScanDefinition(PageAddress.Empty))),
                           new IteratorContext(null!),
                           CancellationToken.None)
                .GetAwaiter()
                .GetResult();

        return iterator;
    }

    private static IReadOnlyList<IRecord> Rows(int count)
        => [.. Enumerable.Range(0, count)
                         .Select(i => Record(new ComputedField("Id", AccessValue.FromInteger(SqlDbType.Int, i)),
                                             new ComputedField("Name", AccessValue.FromBytes(SqlDbType.NVarChar, Encoding.Unicode.GetBytes("x")))))];

    private static IRecord Record(params RecordField[] fields) => new BatchRecord([.. fields], 0, 0);

    private sealed class StubFactory(IIterator iterator) : IIteratorFactory
    {
        public IIterator Create(IteratorDefinition definition) => iterator;

        public IBatchIterator CreateBatch(IteratorDefinition definition) => throw new NotSupportedException();
    }

    private sealed class StubIterator(IReadOnlyList<IRecord> rows) : IIterator
    {
        private int Position { get; set; }

        public int NodeId => 1;

        public IRecord? CurrentRow { get; private set; }

        public bool IsComplete { get; private set; }

        public StopReason? StopReason => IsComplete ? Execution.AccessPaths.Results.StopReason.PageExhausted : null;

        public PageAddress? CurrentPageAddress => null;

        public AccessStrategy? Strategy => null;

        public Task OpenAsync(IteratorDefinition definition, IteratorContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IRecord?> GetRowAsync(CancellationToken cancellationToken)
        {
            if (Position >= rows.Count)
            {
                IsComplete = true;

                CurrentRow = null;

                return Task.FromResult<IRecord?>(null);
            }

            CurrentRow = rows[Position++];

            return Task.FromResult<IRecord?>(CurrentRow);
        }

        public Task CloseAsync() => Task.CompletedTask;
    }
}
