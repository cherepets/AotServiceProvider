namespace AotServiceProvider.Abstractions
{
    public interface IServices
    {
        T Get<T>() where T : class;
        T New<T>() where T : class;
    }
}
