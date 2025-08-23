using System;
using System.Collections.Generic;
using g3;

namespace TT_Lab.AssetData.Instance.Collision;

/// <summary>
/// This is mostly an override for traversal to collect data how Twinsanity wants to see it in the end
/// </summary>
/// <param name="m"></param>
/// <param name="autoBuild"></param>
public class DMeshAabbTreeCustom(DMesh3 m, List<CollisionTriangle> triangles, bool autoBuild = false) : DMeshAABBTree3(m, autoBuild)
{
    private const float BOX_EPS = 5.9604645E-06F;
    private int _traversalBoxId = 0;
    private int _groupId = 0;

    public class TreeTraversalCustom : TreeTraversal
    {
        public Func<int, AxisAlignedBox3f, CollisionTrigger> BoxEnter = (boxId, box) => new CollisionTrigger();
        public Action<int, int, CollisionTrigger> BoxExit = (minBoxId, maxBoxId, trigger) => { };
        public Action<int, int, List<CollisionTriangle>, CollisionTrigger> GroupEnter = (groupId, triangleAmount, triList, trigger) => { };
    }
    
    private AxisAlignedBox3f get_box(int iBox)
    {
        var c = box_centers[iBox];
        var e = box_extents[iBox];
        return new AxisAlignedBox3f(ref c, e.x + BOX_EPS, e.y + BOX_EPS, e.z + BOX_EPS);
    }

    public override void DoTraversal(TreeTraversal traversal)
    {
        _traversalBoxId = 0;
        _groupId = 0;
        base.DoTraversal(traversal);
    }

    protected override void tree_traversal(int iBox, int depth, TreeTraversal traversal)
    {
        var traversalCustom = (TreeTraversalCustom)traversal;
        var idx = box_to_index[iBox];
        
        var boxIndex = _traversalBoxId;
        var trigger = traversalCustom.BoxEnter(boxIndex, get_box(iBox));

        if ( idx < triangles_end )
        {
            // triangle-list case, array is [N t1 t2 ... tN]
            var n = index_list[idx];
            var triList = new List<CollisionTriangle>();
            for (var i = 1; i <= n; ++i)
            {
                triList.Add(triangles[index_list[idx + i]]);
            }
            traversalCustom.GroupEnter(++_groupId, n, triList, trigger);
        }
        else
        {
            
            var i0 = index_list[idx];
            if ( i0 < 0 )
            {
                // negative index means we only have one 'child' box to descend into
                i0 = (-i0) - 1;
                var box = get_box(i0);
                _traversalBoxId++;
                if (traversalCustom.NextBoxF(box, depth + 1))
                {
                    tree_traversal(i0, depth + 1, traversal);
                }
            }
            else
            {
                // positive index, two sequential child box indices to descend into
                i0 -= 1;
                var box = get_box(i0);
                _traversalBoxId++;
                if (traversalCustom.NextBoxF(box, depth + 1))
                {
                    tree_traversal(i0, depth + 1, traversal);
                }
                
                traversalCustom.BoxExit(boxIndex, _traversalBoxId, trigger);

                var i1 = index_list[idx + 1] - 1;
                box = get_box(i1);
                _traversalBoxId++;
                if (traversalCustom.NextBoxF(box, depth + 1))
                {
                    tree_traversal(i1, depth + 1, traversal);
                }
            }
        }
    }
}