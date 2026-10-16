namespace GalgameManager.Helpers;

public class ThreadSafe<T> where T : struct
{
    private readonly object _lockObject = new object();
    private T _value;

    public ThreadSafe() { }

    public ThreadSafe(T initialValue)
    {
        _value = initialValue;
    }

    public T Value
    {
        get
        {
            lock (_lockObject)
            {
                return _value;
            }
        }
        set
        {
            lock (_lockObject)
            {
                _value = value;
            }
        }
    }
}