using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;

using var s = new Scenario();
var solution = new Solution(s);
solution.Execute();
Check.Notices(solution.Changes, new(s.Units[0], ChangeKind.Added, null), new(s.Units[2], ChangeKind.Added, null), new(s.Units[6], ChangeKind.Added, null));
solution.Execute();
Check.That(solution.Changes.Count == 0, "Unchanged tags produced events");
_ = s.World.GetRef<Marked>(s.Units[0]);
s.World.Add(s.Units[1], s.MarkedId);
s.World.Remove(s.Units[2], s.MarkedId);
Entity old = s.Units[6];
s.World.Destroy(old);
Entity added = s.World.Create(s.MarkedId);
solution.Execute();
Check.Notices(solution.Changes, new(s.Units[0], ChangeKind.Modified, null), new(s.Units[1], ChangeKind.Added, null), new(s.Units[2], ChangeKind.Removed, null), new(old, ChangeKind.Removed, null), new(added, ChangeKind.Added, null));
Query tags = s.World.WhereAll(s.MarkedId);
TagWriteCheck.Invoke(s.World, in tags);
solution.Execute();
Check.Notices(solution.Changes, new(s.Units[0], ChangeKind.Modified, null), new(s.Units[1], ChangeKind.Modified, null), new(added, ChangeKind.Modified, null));

Console.WriteLine("PASS 07-tag-events");

namespace ApiChoiceProbe
{
    internal static class TagWriteCheck
    {
        public static void Invoke(World world, in Query tags)
        {
            world.ForEach(in tags, static (ref Marked tag) => { }).Invoke();
        }
    }
}
