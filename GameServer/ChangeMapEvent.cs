using System;

namespace GameServer
{
    public sealed class ChangeMapEvent : EventObject
    {
        public uint TargetMapId { get; }
        public ushort TargetX { get; }
        public ushort TargetY { get; }

        private static readonly object _gateLock = new();
        private static readonly System.Collections.Generic.Dictionary<uint, DateTime> _lastTeleport = new();

        public ChangeMapEvent(uint targetMapId, ushort targetX, ushort targetY)
        {
            TargetMapId = targetMapId;
            TargetX = targetX;
            TargetY = targetY;
        }

        public override void OnEnter(MapObject mapObject)
        {
            base.OnEnter(mapObject);

            if (_disabled)
                return;

            if (mapObject is not HumanPlayer player)
                return;

            var now = DateTime.UtcNow;
            lock (_gateLock)
            {
                if (_lastTeleport.TryGetValue(player.ObjectId, out var last) && (now - last).TotalMilliseconds < 500)
                    return;
                _lastTeleport[player.ObjectId] = now;
            }

            player.AddProcess(ProcessType.ChangeMap, TargetMapId, TargetX, TargetY, 0, 50, 0, null);
        }

        public override ObjectType GetObjectType()
        {
            return ObjectType.Event;
        }
    }
}
