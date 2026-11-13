namespace SmartX.Algorithms;

/// <summary>Balanced, ordered index; inclusive range search prunes unrelated subtrees.</summary>
// (Sedgewick and Wayne, 2011a): balanced search indexes support logarithmic lookup.
// This is an original AVL implementation (height rotations), not their red-black implementation.
public sealed class AvlTree<TKey, TValue> where TKey : IComparable<TKey>
{
    private sealed class Node(TKey key, TValue value)
    {
        public TKey Key = key;
        public TValue Value = value;
        public Node? Left, Right;
        public int Height = 1;
    }
    private Node? root;
    public int Count { get; private set; }
    public int Height => H(root);
    private static int H(Node? n) => n?.Height ?? 0;
    private static void Update(Node n) => n.Height = 1 + Math.Max(H(n.Left), H(n.Right));
    private static Node RightRotate(Node y)
    {
        var x = y.Left!; y.Left = x.Right; x.Right = y; Update(y); Update(x); return x;
    }
    private static Node LeftRotate(Node x)
    {
        var y = x.Right!; x.Right = y.Left; y.Left = x; Update(x); Update(y); return y;
    }
    public void Insert(TKey key, TValue value) => root = Insert(root, key, value);
    private Node Insert(Node? n, TKey key, TValue value)
    {
        if (n is null) { Count++; return new(key, value); }
        int cmp = key.CompareTo(n.Key);
        if (cmp < 0) n.Left = Insert(n.Left, key, value);
        else if (cmp > 0) n.Right = Insert(n.Right, key, value);
        else { n.Value = value; return n; }
        Update(n);
        int balance = H(n.Left) - H(n.Right);
        if (balance > 1)
        {
            if (key.CompareTo(n.Left!.Key) > 0) n.Left = LeftRotate(n.Left);
            return RightRotate(n);
        }
        if (balance < -1)
        {
            if (key.CompareTo(n.Right!.Key) < 0) n.Right = RightRotate(n.Right);
            return LeftRotate(n);
        }
        return n;
    }
    public (List<TValue> Values, int Visited) Range(TKey from, TKey to)
    {
        List<TValue> result = []; int visited = 0;
        Walk(root); return (result, visited);
        void Walk(Node? n)
        {
            if (n is null) return;
            visited++;
            int low = from.CompareTo(n.Key), high = to.CompareTo(n.Key);
            if (low < 0) Walk(n.Left);
            if (low <= 0 && high >= 0) result.Add(n.Value);
            if (high > 0) Walk(n.Right);
        }
    }
    public bool IsBalanced() => Check(root).Valid;
    private static (bool Valid, int Height) Check(Node? n)
    {
        if (n is null) return (true, 0);
        var l = Check(n.Left); var r = Check(n.Right);
        int height = 1 + Math.Max(l.Height, r.Height);
        return (l.Valid && r.Valid && Math.Abs(l.Height-r.Height) <= 1 && n.Height == height, height);
    }
}
/* Reference list:
Sedgewick, R. and Wayne, K. (2011a) Algorithms, 4th edition: Searching.
Available at: https://algs4.cs.princeton.edu/30searching/ (Accessed: 9 November 2026).
*/
