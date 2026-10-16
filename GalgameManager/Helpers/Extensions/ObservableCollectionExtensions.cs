using System.Collections.ObjectModel;

namespace GalgameManager.Helpers;

public static class ObservableCollectionExtensions
{
    /// <summary>
    /// 将collection与other同步
    /// </summary>
    /// <param name="collection">待同步的ObservableCollection</param>
    /// <param name="other">要匹配的列表</param>
    /// <param name="sort">是否要求顺序一致</param>
    /// <typeparam name="T"></typeparam>
    public static void SyncCollection<T>(this ObservableCollection<T> collection, IList<T> other, bool sort = false)
        where T : notnull
    {
        // var delta = other.Count - collection.Count;
        // for (var i = 0; i < delta; i++)
        //     collection.Add(other[0]); //内容不总要，只是要填充到对应的总数
        // for (var i = delta; i < 0; i++)
        //     collection.RemoveAt(collection.Count - 1);
        //
        // for (var i = 0; i < other.Count; i++) 
        //     collection[i] = other[i];

        HashSet<T> toRemove = new(collection.Where(obj => !other.Contains(obj)));
        HashSet<T> toAdd = new(other.Where(obj => !collection.Contains(obj)));
        foreach (T obj in toRemove)
            collection.Remove(obj);
        foreach (T obj in toAdd)
            collection.Add(obj);

        if (!sort) return;
        Dictionary<T, int> index = new();
        for (var i = 0; i < other.Count; i++)
            index[other[i]] = i;
        collection.Sort((a, b) => index[a].CompareTo(index[b]));
    }
    
    public static ObservableCollection<T> Sort<T>(this ObservableCollection<T> collection, Func<T, T, int> cmp)
    {
        List<T> list = new(collection);
        list.Sort((a, b) => cmp(a, b));
        for (var i = 0; i < list.Count; i++)
        {
            if (!list[i]!.Equals(collection[i]))
                collection.Move(collection.IndexOf(list[i]), i);
        }
        return collection;
    }
}