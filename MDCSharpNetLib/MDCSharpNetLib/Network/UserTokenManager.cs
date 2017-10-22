using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace MDCSharpNetLib.Network
{
    class UserTokenManager
    {
        Int64 CurrentCount;

        ConcurrentDictionary<Int64, Session> Users = new ConcurrentDictionary<Int64, Session>();

        Timer TimerHeartbeat;
        long HeartbeatDuration;


        public UserTokenManager() { }


        public void StartHeartbeatChecking(uint check_interval_sec, uint allow_duration_sec)
        {
            HeartbeatDuration = allow_duration_sec * 10000000;
            TimerHeartbeat = new Timer(CheckHeartbeat, null, 1000 * check_interval_sec, 1000 * check_interval_sec);
        }


        public void Add(Session user)
        {
            if (Users.TryAdd(user.UniqueId, user))
            {
                Interlocked.Increment(ref CurrentCount);
            }
        }


        public void Remove(Session user)
        {
            var uniqueId = user.UniqueId;

            if (Users.TryRemove(uniqueId, out var temp))
            {
                Interlocked.Decrement(ref CurrentCount);
            }
        }


        public bool IsExist(Session user)
        {
            return Users.ContainsKey(user.UniqueId);
        }


        public Int64 GetTotalCount()
        {
            Interlocked.Read(ref CurrentCount);
            return CurrentCount;
        }


        void CheckHeartbeat(object state)
        {
            long allowed_time = DateTime.Now.Ticks - this.HeartbeatDuration;

            foreach (var user in Users.Values)
            {
                long heartbeat_time = user.LatestHeartbeatTime;
                if (heartbeat_time >= allowed_time)
                {
                    continue;
                }

                user.DisConnect();
            }
        }
    }
}
