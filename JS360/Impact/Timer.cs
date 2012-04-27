// From http://stackoverflow.com/a/7158557

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Impact
{
    public class Timer
    {
        public Action Trigger;
        public float Interval;
        public bool Continous;
        protected TimerManager manager;
        public int ID;
        float Elapsed;

        public Timer(TimerManager manager, float Interval, bool Continous, Action Trigger)
        { 
            this.manager = manager;
            this.Interval = Interval;
            this.Trigger = Trigger;
            this.Continous = Continous;
        }

        public void Update(float Seconds)
        {
            Elapsed += Seconds;
            if (Elapsed >= Interval)
            {
                Trigger.Invoke();
                if (!Continous)
                    Destroy();
                else
                    Elapsed = 0;
            }
        }

        public void Destroy()
        {
            this.manager.Remove(this);
        }
    }


    public class TimerManager
    {
        List<Timer> ToRemove = null;
        List<Timer> ToAdd = null;
        List<Timer> Timers = null;

        public int CurrentID = 0;

        public TimerManager()
        {
            ToRemove = new List<Timer>();
            ToAdd = new List<Timer>();
            Timers = new List<Timer>();
        }

        public void Add(Timer Timer) { ToAdd.Add(Timer); }
        public void Remove(Timer Timer) { ToRemove.Add(Timer); }
        public void Remove(int ID) {
            foreach (Timer timer in Timers)
            {
                if (timer.ID == ID)
                    ToRemove.Add(timer);
            }
       }

        public void Update(GameTime gametime)
        {        
            foreach (Timer timer in ToAdd) 
                Timers.Add(timer);
            ToAdd.Clear();
            
            foreach (Timer timer in ToRemove) 
                Timers.Remove(timer);
            ToRemove.Clear();

            foreach (Timer timer in Timers) 
                timer.Update((float)gametime.ElapsedGameTime.TotalSeconds);
        }

        public void Create(float Interval, bool Continous, Action Trigger)
        {
            Timer Timer = new Timer(this, Interval, Continous, Trigger);
            Timer.ID = this.CurrentID++;
            this.Add(Timer);
        }
    }
}
