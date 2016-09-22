using Backend.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Backend.ThreeAddressCode.Values;
using Microsoft.Cci;
using ScopeProgramAnalysis.Framework;

namespace Backend.Model
{
    public struct NodeField
    {
        public PTGNode Source { get; set; }
        public IFieldReference Field { get; set; }
        public NodeField(PTGNode source, IFieldReference field)
        {
            this.Source = source;
            this.Field = field;
        }
    }

    // Unknown PTG nodes represent placeholders
    // (external objects that can be null or
    // stand for multiple objects).
    // Useful to model parameter values.
    public enum PTGNodeKind
    {
        Null,
        Object,
        Unknown,
        Parameter,
        Delegate,
        Global
    }


    public interface PTGContext
    {

    }

    public class MethodContex : PTGContext
    {
        public MethodContex(IMethodReference method)
        {
            this.Method = method;
        }
        public IMethodReference Method { get; set; }
        public override string ToString()
        {
            if (Method != null)
            {
                return Method.Name.ToString();
            }
            else return "--";
        }
        public override bool Equals(object obj)
        {
            var oth = obj as MethodContex;
            return oth.Method.Equals(Method);
        }
        public override int GetHashCode()
        {
            return this.Method.GetHashCode();
        }
    }

    public class GlobalContext : PTGContext
    {
        public static GlobalContext NullNodeContext = new GlobalContext();
        public static GlobalContext GlobalNodeContext = new GlobalContext();
    }

    public class PTGID
    {
        public PTGID(PTGContext context, int offset)
        {
            this.Context = context;
            this.OffSet = offset;
        }
        PTGContext Context { get; set; }
        public int OffSet { get; set; }
        public override string ToString()
        {
            return String.Format("{0}:{1:X4}", Context, OffSet);
        }
        public override bool Equals(object obj)
        {
            var ptgID = obj as PTGID;
            return ptgID != null && ptgID.OffSet == OffSet
                && (ptgID.Context == Context || ptgID.Context.Equals(Context));
        }
        public override int GetHashCode()
        {
            if (Context == null) return OffSet.GetHashCode();
            return Context.GetHashCode() + OffSet.GetHashCode();
        }
    }

    public class PTGNode
    {
        public PTGID Id { get; private set; }
        public PTGNodeKind Kind { get; private set; }
        public uint Offset { get; set; }
        public ITypeReference Type { get; set; }
        public ISet<IVariable> Variables { get; private set; }
        public MapSet<IFieldReference, PTGNode> Sources { get; private set; }
        public MapSet<IFieldReference, PTGNode> Targets { get; private set; }

        //public PTGNode(PTGID id, PTGNodeKind kind = PTGNodeKind.Null)
        //      {
        //	this.Id = id;
        //          this.Kind = kind;
        //          this.Variables = new HashSet<IVariable>();
        //          this.Sources = new MapSet<IFieldReference, PTGNode>();
        //          this.Targets = new MapSet<IFieldReference, PTGNode>();
        //      }

        public PTGNode(PTGID id, ITypeReference type, PTGNodeKind kind = PTGNodeKind.Object)
        //	: this(id, kind)
        {
            this.Id = id;
            this.Type = type;
            this.Kind = kind;
            this.Variables = new HashSet<IVariable>();
            this.Sources = new MapSet<IFieldReference, PTGNode>();
            this.Targets = new MapSet<IFieldReference, PTGNode>();
        }

        public bool SameEdges(PTGNode node)
        {
            if (node == null) throw new ArgumentNullException("node");

            return this.Variables.SetEquals(node.Variables) &&
                this.Sources.MapEquals(node.Sources) &&
                this.Targets.MapEquals(node.Targets);
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj)) return true;
            var other = obj as PTGNode;

            return other != null &&
                this.Id.Equals(other.Id) &&
                this.Kind == other.Kind &&
                //this.Offset == other.Offset &&
                this.Type.Equals(other.Type);
        }

        public override int GetHashCode()
        {
            //return this.Id.GetHashCode();
            return this.Id.GetHashCode() + this.Type.GetHashCode() + this.Kind.GetHashCode();
        }

        public override string ToString()
        {
            string result;

            switch (this.Kind)
            {
                case PTGNodeKind.Null:
                    result = "null";
                    break;

                default:
                    result = string.Format("{0:X$}: {1}", this.Id, this.Type);
                    break;
            }

            return result;
        }
        public virtual PTGNode Clone()
        {
            var clone = new PTGNode(this.Id, this.Type, this.Kind);
            return clone;
        }
    }

    public class NullNode : PTGNode
    {
        public static PTGID nullID = new PTGID(GlobalContext.NullNodeContext, 0);

        public NullNode() : base(nullID, MyLoader.PlatformTypes.SystemObject, PTGNodeKind.Null)
        {
        }
        public override bool Equals(object obj)
        {
            var oth = obj as NullNode;
            return oth != null;
        }
        public override int GetHashCode()
        {
            return 0;
        }
        public override string ToString()
        {
            return "Null";
        }
        public override PTGNode Clone()
        {
            return this;
        }
    }

    public class GlobalNode : PTGNode
    {
        public static PTGID globalID = new PTGID(GlobalContext.GlobalNodeContext, -1);

        public GlobalNode() : base(globalID, MyLoader.PlatformTypes.SystemObject, PTGNodeKind.Global)
        {
        }
        public override bool Equals(object obj)
        {
            var oth = obj as GlobalNode;
            return oth != null;
        }
        public override int GetHashCode()
        {
            return 0;
        }
        public override string ToString()
        {
            return "Global";
        }
        public override PTGNode Clone()
        {
            return this;
        }

    }

    public class ParameterNode : PTGNode
    {
        public string Parameter { get; private set; }
        public ParameterNode(PTGID id, string parameter, ITypeReference type, PTGNodeKind kind = PTGNodeKind.Null) : base(id, type, PTGNodeKind.Parameter)
        {
            this.Parameter = parameter;
        }
        public override bool Equals(object obj)
        {
            var oth = obj as ParameterNode;
            return oth != null && oth.Parameter.Equals(Parameter) && base.Equals(oth);
        }
        public override int GetHashCode()
        {
            return this.Parameter.GetHashCode() + base.GetHashCode();
        }
        public override PTGNode Clone()
        {
            var clone = new ParameterNode(this.Id, this.Parameter, this.Type);
            return clone;
        }

    }

    public class DelegateNode : PTGNode
    {
        public IMethodReference Method { get; private set; }
        public IVariable Instance { get; set; }
        public bool IsStatic { get; private set; }

        public DelegateNode(PTGID id, IMethodReference method, IVariable instance) : base(id, method.ReturnType, PTGNodeKind.Delegate)
        {
            this.Method = method;
            this.Instance = instance;
            this.IsStatic = instance == null;
        }
        public override bool Equals(object obj)
        {
            var oth = obj as DelegateNode;
            return oth != null && oth.Method.Equals(Method)
                && oth.Instance == this.Instance
                && base.Equals(oth);
        }
        public override int GetHashCode()
        {
            return this.Method.GetHashCode() + (this.IsStatic ? 1 : this.Instance.GetHashCode())
                + base.GetHashCode();
        }
        public override PTGNode Clone()
        {
            var node = new DelegateNode(this.Id, this.Method, this.Instance);
            return node;
        }
    }

    public class SimplePointsToGraph 
    {
        private Stack<MapSet<IVariable, PTGNode>> stackFrame;
        private MapSet<IVariable, PTGNode> roots;
        private MapSet<NodeField,PTGNode> edges;
        private ISet<PTGNode> nodes;

        public static PTGNode NullNode = new NullNode(); // { get; private set; }
        public static PTGNode GlobalNode = new GlobalNode();


        public SimplePointsToGraph()
        {
            this.stackFrame = new Stack<MapSet<IVariable, PTGNode>>();
            this.roots = new MapSet<IVariable, PTGNode>();
            this.nodes = new HashSet<PTGNode>();
            this.edges = new MapSet<NodeField, PTGNode>();
            this.Add(SimplePointsToGraph.NullNode);
        }

        public IEnumerable<IVariable> Roots
        {
            get { return this.roots.Keys; }
        }

        public IEnumerable<PTGNode> Nodes
        {
            get { return nodes; }
        }

        public bool GraphLessEquals(SimplePointsToGraph other)
        {
            if (object.ReferenceEquals(this, other)) return true;
            return other != null &&
                this.roots.MapLessEquals(other.roots) &&
                this.nodes.IsSubsetOf(other.nodes) &&
                this.edges.MapLessEquals(other.edges);
        }


        public bool GraphEquals(object obj)
        {
            if (object.ReferenceEquals(this, obj)) return true;
            var other = obj as SimplePointsToGraph;

            return other != null &&
                this.roots.MapEquals(other.roots) &&
                this.nodes.SetEquals(other.nodes) &&
                this.edges.MapEquals(other.edges);
        }

        public SimplePointsToGraph Clone()
        {
            var ptg = new SimplePointsToGraph();
            ptg.stackFrame = new Stack<MapSet<IVariable, PTGNode>>(this.stackFrame.Reverse());
            ptg.roots = new MapSet<IVariable, PTGNode>(this.roots);
            ptg.nodes = new HashSet<PTGNode>(this.nodes);
            ptg.edges = new MapSet<NodeField, PTGNode>(this.edges);
            return ptg;
        }

        public SimplePointsToGraph ShalowClone()
        {
            var ptg = new SimplePointsToGraph();
            ptg.stackFrame = this.stackFrame;
            ptg.roots = this.roots;
            ptg.nodes = this.nodes;
            ptg.edges = this.edges;
            return ptg;
        }
        public SimplePointsToGraph Join(SimplePointsToGraph ptg)
        {
            var result = this.Clone();
            this.Union(ptg);
            return result;
        }

        public void Union(SimplePointsToGraph ptg)
        {
            if(stackFrame.Count!=ptg.stackFrame.Count)
            { }
            // We assume they have the same stack frame
            if (this.stackFrame == null && ptg.stackFrame != null)
            {
                this.stackFrame = new Stack<MapSet<IVariable, PTGNode>>(ptg.stackFrame.Reverse());
            }

            this.nodes.UnionWith(ptg.nodes);
            this.roots.UnionWith(ptg.roots);
            this.edges.UnionWith(ptg.edges);

            // Recompute Variables
            // add variable <---> node edges
            foreach (var entry in this.roots)
            {
                foreach(var n in entry.Value)
                {
                    n.Variables.Add(entry.Key);
                }
            }
        }

        public void Add(PTGNode node)
        {
            nodes.Add(node);
        }

        public bool Contains(IVariable variable)
        {
            return this.roots.ContainsKey(variable);
        }

        public bool Contains(PTGNode node)
        {
            return nodes.Contains(node);
        }

        public void Add(IVariable variable)
        {
            roots.Add(variable);
        }

        public void PointsTo(IVariable variable, PTGNode target)
        {
           this.Add(target);
           this.roots.Add(variable, target);

            target.Variables.Add(variable);
        }
        public void PointsTo(IVariable variable, IEnumerable<PTGNode> targets)
        {
            foreach (var n in targets)
            {
                PointsTo(variable, n);
            }
        }

        public void PointsTo(PTGNode source, IFieldReference field, PTGNode target)
        {
            if (source.Equals(SimplePointsToGraph.NullNode))
                return;

            this.nodes.Add(target);
            this.nodes.Add(source);

            var currentTargets = GetTargets(source, field);
            if (currentTargets.Count == 1 && currentTargets.Single() == SimplePointsToGraph.NullNode)
            {
                this.RemoveTargets(source, field);
            }
            this.AddEdge(source, field, target);
        }

        public void AddEdge(PTGNode source, IFieldReference field, PTGNode target)
        {
            var nodeField = new NodeField(source, field);
            this.edges.Add(nodeField, target);
        }


        public ISet<PTGNode> GetTargets(IVariable variable, bool failIfNotExists = false)
        {
            if (failIfNotExists) return roots[variable];

            if (roots.ContainsKey(variable))
                return roots[variable];
            return new HashSet<PTGNode>();
        }
        public ISet<PTGNode> GetTargets(IVariable variable, IFieldReference field)
        {
            var result = new HashSet<PTGNode>();
            //foreach (var ptg in variables[variable])
            foreach (var node in GetTargets(variable, false))
            {
                result.UnionWith(GetTargets(node,field));
            }
            return result;
        }
        public ISet<PTGNode> GetTargets(PTGNode source, IFieldReference field)
        {
            var result = new HashSet<PTGNode>();
            var nodeField = new NodeField(source, field);
            if (this.edges.ContainsKey(nodeField))
            { 
                result.UnionWith(this.edges[nodeField]);
            }
            return result;
        }
        public MapSet<IFieldReference, PTGNode> GetTargets(PTGNode ptgNode)
        {
            //var result = this.edges.Where(kv => kv.Key.Source.Equals(ptgNode)).GroupBy(kv => kv.Key.Field, kv=> kv.Value);
            //return result.ToDictionary( kv => kv.Key, kv => kv.SelectMany(kv => kv.));
            var result = new MapSet<IFieldReference, PTGNode>();
            foreach (var edge in this.edges.Where(kv => kv.Key.Source.Equals(ptgNode)))
            {
                result.AddRange(edge.Key.Field, edge.Value);
            }
            return result;
        }

        public void RemoveTargets(PTGNode source, IFieldReference field)
        {
            var nodeField = new NodeField(source, field);
            this.edges.Remove(nodeField);
        }

        public void RemoveRootEdges(IVariable variable)
        {
            var hasVariable = this.Contains(variable);
            if (!hasVariable) return;
            var targets = this.roots[variable];
            targets.Clear();
        }

        #region Handling of method scopes

        public MapSet<IVariable, PTGNode> NewFrame()
        {
            if (this.stackFrame == null)
            {
                this.stackFrame = new Stack<MapSet<IVariable, PTGNode>>();
            }
            var result = roots;
            foreach (var entry in roots)
            {
                var nodes = entry.Value;
                foreach (var node in nodes)
                {
                    node.Variables.Remove(entry.Key);
                }
            }

            var frame = new MapSet<IVariable, PTGNode>();
            stackFrame.Push(roots);
            roots = frame;
            return result;
        }

        public void CleanUnreachableNodes()
        {
            var reacheableNodes = this.ReachableNodesFromVariables();
            var unreacheableNodes = this.nodes.Except(reacheableNodes);

            var edgesToRemove = new HashSet<NodeField>();
            foreach(var entry in this.edges)
            {
                if(unreacheableNodes.Contains(entry.Key.Source))
                {
                    edgesToRemove.Add(entry.Key);
                }
            }
            foreach (var entry in edgesToRemove)
            {
                this.edges.Remove(entry);
            }
            //this.nodes.ExceptWith(unreacheableNodes);
            this.nodes = new HashSet<PTGNode>(this.nodes.Except(unreacheableNodes));
        }


        public MapSet<IVariable, PTGNode> NewFrame(IEnumerable<KeyValuePair<IVariable, IVariable>> binding)
        {
            var oldFrame = NewFrame();
            foreach (var entry in binding)
            {
                if (oldFrame.ContainsKey(entry.Key))
                {
                    this.roots.Add(entry.Value);
                    foreach (var node in oldFrame[entry.Key])
                    {
                        PointsTo(entry.Value, node);
                    }
                }
            }
            return oldFrame;
        }

        public void RestoreFrame(bool cleanUnreachable = true)
        {
            var frame = stackFrame.Pop();
            foreach (var entry in roots)
            {
                var nodes = entry.Value;
                foreach (var node in nodes)
                {
                    node.Variables.Clear();
                }
            }
            roots = frame;
            foreach (var entry in roots)
            {
                var nodes = entry.Value;
                foreach (var node in nodes)
                {
                    node.Variables.Add(entry.Key);
                }
            }
            if (cleanUnreachable)
                CleanUnreachableNodes();
        }

        public void RestoreFrame(IVariable retVariable, IVariable dest, bool cleanUnreachable = true)
        {
            ISet<PTGNode> nodes = null;

            var validReturn = (dest.Type != null && dest.Type.IsClassOrStruct()); //(retVariable.Type != null && retVariable.Type.TypeKind == TypeKind.ReferenceType)  &&

            if (validReturn)
                nodes = GetTargets(retVariable);

            RestoreFrame(false);
            if (validReturn)
            {
                PointsTo(dest, nodes);
            }
            if (cleanUnreachable)
                CleanUnreachableNodes();
        }

        #endregion

        #region Reacheability, aliasing
        public IEnumerable<PTGNode> ReachableNodesFromVariables()
        {
            var ptg = this;
            var roots = new HashSet<PTGNode>(ptg.Roots.SelectMany(v => ptg.GetTargets(v, false)));
            roots.Add(SimplePointsToGraph.NullNode);
            return ptg.ReachableNodes(roots);
        }
        public IEnumerable<PTGNode> ReachableNodes(IEnumerable<PTGNode> roots,
                                                          Predicate<Tuple<PTGNode, IFieldReference>> filter = null)
        {
            var ptg = this;
            // var result = new HashSet<PTGNode>();
            ISet<PTGNode> visitedNodes = new HashSet<PTGNode>();
            Queue<PTGNode> workList = new Queue<PTGNode>();

            foreach (var ptgNode in roots)
            {
                workList.Enqueue(ptgNode);
            }
            while (workList.Any())
            {
                var ptgNode = workList.Dequeue();
                visitedNodes.Add(ptgNode);
                if (ptgNode.Equals(SimplePointsToGraph.NullNode))
                {
                    continue;
                }
                foreach (var adjacents in ptg.GetTargets(ptgNode))
                {
                    if (filter != null)
                    {
                        var node_filter = Tuple.Create(ptgNode, adjacents.Key);
                        if (!filter(node_filter))
                            continue;
                    }

                    foreach (var adjacent in adjacents.Value)
                    {
                        if (!visitedNodes.Contains(adjacent))
                        {
                            workList.Enqueue(adjacent);
                        }
                    }
                }
            }
            return visitedNodes;
        }


        public bool MayReacheableFromVariable(IVariable v1, IVariable v2)
        {
            var ptg = this;
            var result = ptg.GetTargets(v2, false).Any(n => ptg.Reachable(v1, n));
            return result;
        }

        public bool Reachable(IVariable v1, PTGNode n)
        {
            var ptg = this;
            var result = false;
            ISet<PTGNode> visitedNodes = new HashSet<PTGNode>();
            Queue<PTGNode> workList = new Queue<PTGNode>();
            var nodes = ptg.GetTargets(v1, false);

            if (nodes.Contains(n) && !n.Equals(SimplePointsToGraph.NullNode))
                return true;

            foreach (var ptgNode in nodes)
            {
                workList.Enqueue(ptgNode);
            }
            while (workList.Any())
            {
                var ptgNode = workList.Dequeue();
                visitedNodes.Add(ptgNode);
                if (ptgNode.Equals(SimplePointsToGraph.NullNode))
                {
                    continue;
                }
                if (ptgNode.Equals(n)) return true;
                foreach (var adjacents in ptg.GetTargets(ptgNode).Values)
                {
                    foreach (var adjacent in adjacents)
                    {
                        if (!visitedNodes.Contains(adjacent))
                        {
                            workList.Enqueue(adjacent);
                        }
                    }
                }
            }
            return result;
        }

        public ISet<IVariable> GetAliases(IVariable v)
        {
            var ptg = this;
            var res = new HashSet<IVariable>() { v };
            foreach (var ptgNode in ptg.GetTargets(v, false)) // GetPtgNodes(v))
            {
                if (ptgNode != SimplePointsToGraph.NullNode)
                {
                    res.UnionWith(ptgNode.Variables);
                }
            }
            return res;
        }

        #endregion

    }
}
