namespace Sunfish.Foundation.Data;

public interface IDataAdapter<T>
{
    Task<DataResult<T>> GetDataAsync(DataRequest request, CancellationToken cancellationToken = default);
}
