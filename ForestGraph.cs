using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

/// <summary> This is a generic data structure that represents a forest. This is a graph where every
/// subgraph is a part of a tree. In this data structure, we store TStored in this structure, where there are direct parent-child
/// relationships. There is a list of "root" nodes, which are the entries that have no parents.
///
/// <para>The way it works:
/// 1) AddNodeToRoot/DeleteNode to add/remove nodes. Nodes can only be added directly to the root.
/// 2) Use ConnectChild/DisconnectChild to modify the connections of "what is the child of what." 
/// 3) Then, we can query the forest to: see its contents, check connectivity, check how deep an entry is, find parents/children,
/// and efficiently enumerate through all the contents. </para> </summary>
/// <typeparam name="TStored">The type stored in the forest. Must be a class so we can check == null.</typeparam>
public class ForestGraph<TStored> : IEnumerable<TStored> where TStored : class {
    /// <summary> List of all the nodes in the root (ie have no parent) </summary>
    protected List<TreeNode> rootNodes = new List<TreeNode>();
    /// <summary> Directory that links every entry we are storing to its node. </summary>
    protected Dictionary<TStored, TreeNode> nodeDirectory = new Dictionary<TStored, TreeNode>();

    /// <summary> This is the main interior data structure that 1) holds the entry in the forest,
    /// and 2) holds information about what it is immediately connected to. This is like
    /// a single entry of a doubly linked list. Don't expose this. </summary>
    protected class TreeNode {
        /// <summary> What is actually stored in the node. </summary>
        public TStored self;
        /// <summary> The node which is our direct current parent. == null => we have no parent, and are in root. </summary>
        public TreeNode currentParent = null;
        /// <summary> List of all nodes which are DIRECT children. No grandchildren etc. </summary>
        public List<TreeNode> children = new List<TreeNode>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TreeNode(TStored self) => this.self = self;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator TStored(TreeNode node) => node.self;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => self.ToString();
    }

    #region Basic accessors (typical of IEnumerables)
    /// <summary> True if forest contains this entry. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(TStored content) => nodeDirectory.ContainsKey(content);
    /// <summary> Total entries in forest </summary>
    public int Count => nodeDirectory.Count;
    /// <summary> Clear the contents of the forest. </summary>
    public virtual void Clear() { nodeDirectory.Clear(); rootNodes.Clear(); }

    /// <summary> Give the current parent of this. If no parent (in root), return null. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TStored ParentOf(TStored child) => nodeDirectory[child].currentParent?.self;

    /// <summary> Give the number of direct children this parent has. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DirectChildCountOf(TStored parent) => nodeDirectory[parent].children.Count;

    /// <summary> Give an IEnumerable to enumerate through all of an entry's DIRECT children. </summary>
    public IEnumerable<TStored> DirectChildrenOf(TStored parent) {
        TreeNode parentNode = nodeDirectory[parent];
        foreach (TreeNode childNode in parentNode.children)
            yield return childNode.self;
    }
    /// <summary> Return the number of parents this entry has. </summary>
    public int GetDepthOf(TStored entry) {
        int depth = 0;
        foreach (TStored x in AllParentsOf(entry)) depth++;
        return depth;
    }
    #endregion

    #region Order-based accessors
    /// <summary>Compare stored objects obj1 and obj2. Return the -1 if obj1 comes out first, and 1 if obj2 comes out first,
    /// when enumerating breadth first. This isn't super fast, so don't spam this.</summary>
    public int CompareBreadthFirstOrder(TStored obj1, TStored obj2) { // TODO: Untested.
        Debug.Assert(nodeDirectory.ContainsKey(obj1), "First argument isn't in the forest graph!");
        Debug.Assert(nodeDirectory.ContainsKey(obj2), "First argument isn't in the forest graph!");
        Debug.Assert(obj1 != obj2, "Can't compare two of the same object! I could return 0, but this is probably a mistake!");

        List<TreeNode> nodeLineage1 = AllParentNodesIncluding(nodeDirectory[obj1]);
        List<TreeNode> nodeLineage2 = AllParentNodesIncluding(nodeDirectory[obj2]);

        // If different length, the longer one comes later.
        if (nodeLineage1.Count != nodeLineage2.Count) return nodeLineage1.Count.CompareTo(nodeLineage2.Count);
        // Same length, so we need to find the first parent that is different between the two.
        for (int i = nodeLineage1.Count - 1; i > -1; i--) {
            if (nodeLineage1[i] != nodeLineage2[i]) { // Found earliest parent that was different! Compare the parents' order.
                return CompareSiblingNodes(nodeLineage1[i], nodeLineage2[i]);
            }
        }

        int CompareSiblingNodes(TreeNode lhs, TreeNode rhs) {
            foreach (TreeNode node in lhs.currentParent.children) {
                if (node == lhs) return -1;
                else if (node == rhs) return 1;
            }
            Debug.LogError("We should have found the two nodes as siblings before that loop is done!");
            return 0;
        }

        Debug.LogError("The comparison method should have resolved its comparison before that loop ends!");
        return 0;
    }


    #endregion

    #region Modify the Contents of the Forest
    /// <summary> Add this object as a new node into the trees. It will start as a node in the root. </summary>
    public virtual void AddAtRoot(TStored obj) {
        Debug.Assert(obj != null, "We can't add a null object to the forest!");
        Debug.Assert(!nodeDirectory.ContainsKey(obj), "Trying to add something that is already in the forest!");
        TreeNode newNode = new TreeNode(obj);
        rootNodes.Add(newNode);
        nodeDirectory[obj] = newNode;
    }

    /// <summary> Assuming parent and child are already registered in tree, establish a new connection from parent to child. </summary>
    public virtual void ConnectChild(TStored parent, TStored child) {
        Debug.Assert(nodeDirectory.ContainsKey(child), "Child isn’t listed in the forest as a node!");
        Debug.Assert(nodeDirectory.ContainsKey(parent), "Parent isn’t listed in the forest as a node!");

        TreeNode childNode = nodeDirectory[child];
        TreeNode parentNode = nodeDirectory[parent];

        Debug.Assert(childNode.currentParent == null, "Trying to connect a child to a parent, but it already has a parent! Must call disconnect first!");
        Debug.Assert(!AllParentsOf(parentNode.self).Contains(child), "We're trying to connect a child to a parent, but that child is a " +
            "parent (of parent of parent) of that child! Incest/cycle violate the structure of the graph! At " + parent + " and " + child);

        parentNode.children.Add(childNode);
        childNode.currentParent = parentNode;
        rootNodes.Remove(childNode);
    }

    /// <summary> Remove the connection (only the connection) from parent to child. This puts the child into the root. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void DisconnectFromParent(TStored child) => DisconnectFromParent(nodeDirectory[child]);
    /// <summary> Remove the connection (only the connection) from parent to child. </summary>
    protected void DisconnectFromParent(TreeNode childNode) {
        TreeNode parentNode = childNode.currentParent;

        Debug.Assert(!(parentNode is null), "Trying to disconnect child from parent, but it doesn't have a parent!");
        parentNode.children.Remove(childNode);
        rootNodes.Add(childNode);
        childNode.currentParent = null;
    }

    /// <summary> Remove the node associated with this entry. Update tree to reflect it. </summary>
    public void DeleteNode(TStored toDelete) {
        Debug.Assert(nodeDirectory.ContainsKey(toDelete), "Trying to remove a node, but it isn't in the forest!");
        TreeNode nodeToDel = nodeDirectory[toDelete];
        for (int i = nodeToDel.children.Count - 1; i > -1; i--) {
            DisconnectFromParent(nodeToDel.children[i]);
        }
        if (!(nodeToDel.currentParent is null)) nodeToDel.currentParent.children.Remove(nodeToDel);

        nodeDirectory.Remove(toDelete);
        if (nodeToDel.currentParent is null) rootNodes.Remove(nodeToDel);
    }

    /// <summary> Sort the tree and all lists included following this function. This is a Func&lt;TStored, TStored, int&gt;
    /// that outputs 0 for x=y, -1 for x&lt;y, and 1 for y&lt;x. compare could be passed in as a Func instead of a Comparison.</summary>
    public void SortTree(Comparison<TStored> compare) {
        Comparison<TreeNode> nodeCompare = new Comparison<TreeNode>((TreeNode x, TreeNode y) => compare(x.self, y.self));
        //foreach (TreeNode node in EnumerateBackwardsGraph())
        //    node.children.Sort(nodeCompare); // Enumerate backwards because we're modifying the lists.

        SortTree(nodeCompare);
    }
    /// <summary> Sort the tree and all lists included following this function. This is a Func&lt;TStored, TStored, int&gt;
    /// that outputs 0 for x=y, -1 for x&lt;y, and 1 for y&lt;x. compare could be passed in as a Func instead of a Comparison.</summary>
    protected virtual void SortTree(Comparison<TreeNode> nodeCompare) {
        rootNodes.Sort(nodeCompare);
        foreach (TreeNode node in EnumerateBackwardsGraph())
            node.children.Sort(nodeCompare); // Enumerate backwards because we're modifying the lists.
    }
    #endregion

    #region Enumerate From one node
    /// <summary> Return this node AND all the children of it </summary>
    private IEnumerable<TreeNode> EnumThroughChild(TreeNode parent) {
        yield return parent;
        foreach (TreeNode childNode in parent.children) {
            foreach (TreeNode eachNode in EnumThroughChild(childNode)) {
                yield return eachNode;
            }
        }
        yield break;
    }


    /// <summary> Enumerate through ALL children of the input obj, including grandchildren etc </summary>
    public IEnumerable<TStored> AllChildrenOf(TStored parent) {
        IEnumerator<TreeNode> enumeratorWithSelf = EnumThroughChild(nodeDirectory[parent]).GetEnumerator();

        enumeratorWithSelf.MoveNext(); // Skip self
        while (enumeratorWithSelf.MoveNext()) {
            yield return enumeratorWithSelf.Current.self;
        }
        yield break;
    }

    /// <summary> Enumerate through all parents of the input obj, in order of (farthest from root)
    /// to root. If no parents, we’re just done.</summary>
    public IEnumerable<TStored> AllParentsOf(TStored obj) {
        TreeNode current = nodeDirectory[obj];
        while (current.currentParent != null) {
            current = current.currentParent;
            yield return current.self;
        }
        yield break;
    }

    /// <summary> Give a list of all in order of all treenodes that are a parent of this node
    /// in order of self to root. INCLUDE self.</summary>
    private List<TreeNode> AllParentNodesIncluding(TreeNode node) { // TODO better name
        List<TreeNode> allParents = new List<TreeNode>() { node};
        TreeNode current = node;
        while (current.currentParent != null) {
            current = current.currentParent;
            allParents.Add(current);
        }
        return allParents;
    }

    #endregion

    #region Enumerate through whole Forest
    /// <summary> Enumerate through whole forest, including root, going breadth-first, and in the order of all the lists' order. </summary>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    /// <summary> Enumerate through whole forest, including root, going breadth-first, and in the order of all the lists' order. </summary>
    public IEnumerator<TStored> GetEnumerator() => EnumerateBreadthFirst().GetEnumerator();

    /// <summary> Enumerate through whole forest, including root, going depth-first, and in the order of all the lists' order. </summary>
    public IEnumerable<TStored> EnumerateDepthFirst() {
        foreach (TreeNode node in GetGraphEnumerable())
            yield return node.self;
    }
    /// <summary> Enumerate through whole forest, including root, going depth-first, and in the order of all the lists' order. Return tree nodes. </summary>
    protected IEnumerable<TreeNode> GetGraphEnumerable() {
        foreach (TreeNode root in rootNodes) {
            foreach (TreeNode node in EnumThroughChild(root)) {
                yield return node;
            }
        }
    }


    /// <summary> Enumerate through forest breadth-first, in the order in which it was sorted. Is weirdly faster than depth-first. </summary>
    public IEnumerable<TStored> EnumerateBreadthFirst() {
        foreach (TreeNode node in GetGraphEnumerableBreadthFirst())
            yield return node.self;
    }
    private Queue<TreeNode> breadthFirstQueue = new Queue<TreeNode>();
    /// <summary> Enumerate through whole forest, including root, going BREADTH-first, and in the order of all the lists' order. Return tree nodes. </summary>
    private IEnumerable<TreeNode> GetGraphEnumerableBreadthFirst() {
        breadthFirstQueue.Clear();
        // Queue up root.
        foreach (TreeNode root in rootNodes) {
            breadthFirstQueue.Enqueue(root);
        }
        // Go through, queuing up children as we get to them.
        while (breadthFirstQueue.Count > 0) {
            TreeNode node = breadthFirstQueue.Dequeue();
            yield return node;
            // queue children in order to do later.
            foreach (TreeNode child in node.children) {
                breadthFirstQueue.Enqueue(child);
            }
        }
    }

    /// <summary> Enumerate through the whole tree (depth-first), while returning a tuple, where the
    /// second entry is the depth from root. Root = 0. </summary>
    public IEnumerable<Tuple<TStored, int>> EnumerateWithDepth() {
        foreach (TreeNode root in rootNodes) {
            foreach (Tuple<TreeNode, int> nodeDepth in EnumThroughChildWithDepth(root, 0)) {
                yield return new Tuple<TStored, int>(nodeDepth.Item1.self, nodeDepth.Item2);
            }
        }

        IEnumerable<Tuple<TreeNode, int>> EnumThroughChildWithDepth(TreeNode node, int currentDepth) {
            yield return new Tuple<TreeNode, int>(node, currentDepth);

            foreach (TreeNode childNode in node.children) {
                foreach (Tuple<TreeNode, int> each in EnumThroughChildWithDepth(childNode, currentDepth + 1)) {
                    yield return each;
                }
            }
        }
    }


    /// <summary> Enumerate through the whole tree, while returning a tuples of all (Parent, Child) pairs.
    /// Enumerate in a breadth-first way, so everything comes out in order of the sorting of the lists, and depth. </summary>
    public IEnumerable<Tuple<TStored, TStored>> EnumerateParentChildPairs() { // TODO: Test
        breadthFirstQueue.Clear();
        // Queue up only nodes with parents.
        foreach (TreeNode root in rootNodes) {
            foreach (TreeNode child in root.children) {
                breadthFirstQueue.Enqueue(child);
            }
        }
        // Go through, queuing up children as we get to them.
        while (breadthFirstQueue.Count > 0) {
            TreeNode node = breadthFirstQueue.Dequeue();
            yield return new Tuple<TStored, TStored>(node.currentParent.self, node.self);
            foreach (TreeNode child in node.children) {
                breadthFirstQueue.Enqueue(child);
            }
        }
    }


    /// <summary> Enumerate through forest backwards. Go from farthest child to root, in the reverse order of the lists. </summary>
    public IEnumerable<TStored> EnumerateBackwards() {
        foreach (TreeNode node in EnumerateBackwardsGraph())
            yield return node.self;
    }
    /// <summary> Enumerate through forest backwards. Go from farthest child to root, in the reverse order of the lists. </summary>
    private IEnumerable<TreeNode> EnumerateBackwardsGraph() {
        for (int i = rootNodes.Count - 1; i > -1; i--) {
            foreach (TreeNode node in EnumWithChildrenBackwards(rootNodes[i])) {
                yield return node;
            }
        }
        // Return this node and all its children, backwards.
        IEnumerable<TreeNode> EnumWithChildrenBackwards(TreeNode parent) {
            for (int i = parent.children.Count - 1; i > -1; i--) {
                foreach (TreeNode child in EnumWithChildrenBackwards(parent.children[i])) {
                    yield return child;
                }
            }
            yield return parent;
        }
    }
    #endregion

    #region Debugging Aids
    /// <summary> Print a big string for the whole connectivity of the forest. </summary>
    public override string ToString() {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"Full forest: (Total in root = {rootNodes.Count})\n");
        foreach (Tuple<TStored, int> nodeData in EnumerateWithDepth()) {
            PrintObj(nodeData.Item1, nodeData.Item2);
        }

        // Add string for just this one.
        void PrintObj(TStored current, int stepsFromRoot) {
            sb.Append(string.Join("    ", new string[stepsFromRoot + 1]) // Indent
                    + ((stepsFromRoot == 0) ? ">" : "-")); // Use different bullet for roots,
            sb.Append(current == null ? "null" : current.ToString());

            TreeNode node = nodeDirectory[current];
            sb.Append($" ({node.children.Count} dependents)\n");
        }

        return sb.ToString();
    }

#if UNITY_EDITOR
    /// <summary> Print all the contents of the forest using various enumerators. This is mostly
    /// used to check for debugging purposes. </summary>
    private void DebugTestEnumerators() {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("Printing forest showing various representations:\n\n Made by enumerate with depth:\n");
        sb.Append(ToString());

        // Dictionary with everything to append before (ie depth for indent).
        Dictionary<TStored, string> nodeStrings = new Dictionary<TStored, string>();
        foreach (Tuple<TStored, int> nodeData in EnumerateWithDepth()) {
            string currentString = string.Join("    ", new string[nodeData.Item2 + 1]) // Indent
                    + ((nodeData.Item2 == 0) ? ">" : "-"); // Use different bullet for roots,
            currentString += nodeData.Item1 == null ? "null" : nodeData.Item1.ToString();
            Debug.Assert(!nodeStrings.ContainsKey(nodeData.Item1), nodeData.Item1 +
                " is a duplicate in the forest! we should not run a test suite on this!");
            nodeStrings[nodeData.Item1] = currentString;
        }

        sb.Append("\nMain (depth-first) Enumerator :\n");
        foreach (TStored node in this) {
            sb.Append(nodeStrings[node]); sb.Append("\n");
        }

        sb.Append("\n Breadth-First Enumerator :\n");
        foreach (TStored node in EnumerateBreadthFirst()) {
            sb.Append(nodeStrings[node]); sb.Append("\n");
        }


        sb.Append("\nIterate Backwards:\n");
        foreach (TreeNode node in EnumerateBackwardsGraph()) {
            sb.Append(nodeStrings[node.self]); sb.Append("\n");
        }


        IEnumerator<TreeNode> enumCheck = GetGraphEnumerable().GetEnumerator();
        sb.Append("\n\nIndividual node spot checks :\nFirst root node: ");
        enumCheck.MoveNext();
        PrintParentAndChildren(enumCheck.Current);

        sb.Append("\n\nSecond node:");
        enumCheck.MoveNext();
        PrintParentAndChildren(enumCheck.Current);

        sb.Append("\n\nTerminal node:");
        Tuple<TStored, int> farthestNode = new Tuple<TStored, int>(null, -1);
        foreach (Tuple<TStored, int> depthCheck in EnumerateWithDepth()) {
            if (depthCheck.Item2 > farthestNode.Item2) farthestNode = depthCheck;
        }
        PrintParentAndChildren(nodeDirectory[farthestNode.Item1]);


        void PrintParentAndChildren(TreeNode node) {
            sb.Append($"Analyzing {node}:\n     Direct children: ");
            foreach (TStored child in DirectChildrenOf(node.self)) {
                AddSimpleNodeString(child);
            }
            sb.Append($"\n     All children: ");
            foreach (TStored child in AllChildrenOf(node.self)) {
                AddSimpleNodeString(child);
            }
            sb.Append($"\n     Parent: " + (node.currentParent == null ? "null" : node.currentParent));
            sb.Append($"\n     All Parents: ");
            foreach (TStored parent in AllParentsOf(node.self)) {
                AddSimpleNodeString(parent);
            }

            void AddSimpleNodeString(TStored node) => sb.Append((node == null ? "null" : node.ToString()) + ", ");
        }

        Debug.Log(sb.ToString());
    }

    /// <summary> This is a method to just stress test. (and also show off the basics of how this works). </summary>
    public static void DebugTestForestGraph() {
        ForestGraph<string> testForest = new ForestGraph<string>();

        // Populate it. Use strings that are easy to trace where they are supposed to be.
        // Throw in some twists and turns
        testForest.AddAtRoot("A");
        testForest.AddAtRoot("AA"); testForest.ConnectChild("A", "AA");
        testForest.AddAtRoot("AB"); testForest.ConnectChild("A", "AB");
        testForest.AddAtRoot("AAA"); testForest.ConnectChild("AA", "AAA");
        testForest.AddAtRoot("AAB"); testForest.ConnectChild("AA", "AAB");
        testForest.AddAtRoot("AAAA"); testForest.ConnectChild("AAA", "AAAA");

        testForest.AddAtRoot("B");
        testForest.AddAtRoot("BA"); testForest.ConnectChild("B", "BA");
        testForest.AddAtRoot("BB"); testForest.ConnectChild("B", "BB");
        testForest.AddAtRoot("BC"); testForest.ConnectChild("B", "BC");
        testForest.AddAtRoot("BD"); testForest.ConnectChild("B", "BD");

        testForest.AddAtRoot("BCA"); testForest.ConnectChild("BC", "BCA");
        testForest.AddAtRoot("BCAA"); testForest.ConnectChild("BCA", "BCAA");
        testForest.AddAtRoot("BCAAA"); testForest.ConnectChild("BCAA", "BCAAA");
        testForest.AddAtRoot("BCAAAA"); testForest.ConnectChild("BCAAA", "BCAAAA");
        testForest.AddAtRoot("BCAAAB"); testForest.ConnectChild("BCAAA", "BCAAAB");

        testForest.AddAtRoot("C");
        testForest.AddAtRoot("D");
        testForest.AddAtRoot("E");

        // Now we test and print
        testForest.DebugTestEnumerators();

        /////// Edit forest

        // Delete
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        testForest.DeleteNode("BC");
        sb.Append("\nAfter removing BC: ");
        sb.Append(testForest.ToString());

        // Disconnect
        testForest.DisconnectFromParent("BCAA");
        sb.Append("\n\nAfter disconnecting BCAA from BCA: \n");
        sb.Append(testForest.ToString());

        // Reconnect
        testForest.ConnectChild("AA", "BCAA");
        sb.Append("\n\nAfter connecting BCAA to AA: \n");
        sb.Append(testForest.ToString());

        // Sorting
        int ReverseSortString(string a, string b) => -a.CompareTo(b);
        testForest.SortTree(ReverseSortString);
        sb.Append("\n\nAfter reverse sort: \n");
        sb.Append(testForest.ToString());

        testForest.SortTree((string x, string y) => x.CompareTo(y));
        sb.Append("\n\nAfter re-correct sort: \n");
        sb.Append(testForest.ToString());

        Debug.Log(sb.ToString());
    }
#endif

    #endregion

}


#region Sorted Forest
// TODO: Untested
/// <summary>This is a ForestGraph that automatically maintains a list of points where the forest
/// has been modified to make sorting it more efficient.</summary>
public class ForestGraphSorted<TStored> : ForestGraph<TStored> where TStored : class {
    /// <summary>This is a function to automatically compare when sorting.</summary>
    private Comparison<TreeNode> defaultCompare = null;
    /// <summary>Assign a function to be automatically used for sorting the forest.</summary>
    public void AssignDefaultComparison(Comparison<TStored> compare) {
        defaultCompare = new Comparison<TreeNode>((TreeNode x, TreeNode y) => compare(x.self, y.self));
        newCompareAssigned = true;
    }

    /// <summary> List of all tree</summary>
    private List<TreeNode> nodesPendingSort = new();
    /// <summary> TRUE => we still need to sort root.</summary>
    private bool rootSortPending = false;
    /// <summary>This is set true whenever a new comparison is assigned, to make us sort the whole forest. </summary>
    private bool newCompareAssigned = false;
    /// <summary> TRUE => Forest is fully sorted. </summary>
    public bool IsSorted => !rootSortPending && nodesPendingSort.Count == 0 && !newCompareAssigned;

    public ForestGraphSorted(Comparison<TStored> compare) => AssignDefaultComparison(compare);

    #region Extend parent graph-modifying methods
    public override void Clear() {
        base.Clear();
        nodesPendingSort.Clear();
        rootSortPending = false;
    }

    public override void AddAtRoot(TStored obj) {
        base.AddAtRoot(obj);
        rootSortPending = true;
    }

    public override void ConnectChild(TStored parent, TStored child) {
        base.ConnectChild(parent, child);
        nodesPendingSort.Add(nodeDirectory[parent]);
        rootSortPending = true;
    }

   public override void DisconnectFromParent(TStored child) {
        TreeNode childNode = nodeDirectory[child];
        nodesPendingSort.Add(childNode.currentParent);
        base.DisconnectFromParent(childNode);
        rootSortPending = true;
    }
    #endregion

    #region Unique sorting methods
    /// <summary> Sort the tree and all lists, trying to sort just points that changed,
    /// using the comparison assigned into the forest. </summary>
    public void SortTree() {
        // If we assigned a new default comparison (or we have a LOT of nodes to deal with,
        // then we just need to sort the whole graph.
        if (newCompareAssigned
            || (nodesPendingSort.Count > Count / 2)) {
            base.SortTree(defaultCompare);
            newCompareAssigned = false;
            rootSortPending = false;
            nodesPendingSort.Clear();
            return;
        }

        if (rootSortPending) {
            rootNodes.Sort(defaultCompare);
        }

        // Sort each pending node, and use .Distinct to try to avoid double sorting.
        foreach (TreeNode node in nodesPendingSort.Distinct()) {
            node.children.Sort(defaultCompare);
        }

        rootSortPending = false;
        nodesPendingSort.Clear();
    }
    #endregion

    /// <summary> Assert that the graph is currently sorted. Hard full check.
    /// Only used for debugging purposes.</summary>
    public void DebugCheckSorted() {
        foreach (TreeNode node in GetGraphEnumerable()) {
            List<TreeNode> expectedList = node.children.ToList();
            expectedList.Sort(defaultCompare);
            Debug.Assert(expectedList.SequenceEqual(node.children), "Children of " + node + " are not sorted!");
        }

        List<TreeNode> expectedRoot = rootNodes.ToList();
        expectedRoot.Sort(defaultCompare);
        Debug.Assert(expectedRoot.SequenceEqual(rootNodes), "Root is not sorted.");
    }

}
#endregion
