﻿using GUILib.ui;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace GUILib.data {
    class AsyncJob {

        private Func<Object> runAsync;
        private Action<Object> finishSync;

        public AsyncJob(Func<Object> runAsync, Action<Object> finishSync) {
            this.runAsync = runAsync;
            this.finishSync = finishSync;
        }

        public void Run() {
            AsyncManager.GetInstance().AddJob(ExecuteAsync);
        }

        private void ExecuteAsync(Action<Action> runSync) {
            Object result = null;
            try {
                result = runAsync();
            } catch (Exception e) {
                result = e;
            }
            try {
                Action runS = () => {
                    try {
                        this.finishSync(result);
                    } catch (Exception) {
                    }
                };
                runSync(runS);
            } catch (Exception) {
            }
        }
        
    }

    public class AsyncManager {
        
        private static AsyncManager instance = new AsyncManager();

        BlockingCollection<Action<Action<Action>>> jobs = new BlockingCollection<Action<Action<Action>>>();

        private MainContent mc;

        public static void Bootstrap(MainContent mc) {
            GetInstance().mc = mc;
        }

        public void Stop() {
            jobs.Add(null);
        }

        private void Worker() {
            Action<Action> runSync = (acx) => {
                mc.Dispatcher.Invoke(acx);
            };

            while (true) {
                Action<Action<Action>> job = jobs.Take();
                if (job == null) {
                    return;
                } else {
                    try {
                        job(runSync);
                    } catch (Exception) { 
                    }
                }
            }
        }

        private AsyncManager() {
            Thread thread = new Thread(new ThreadStart(Worker));
            thread.Start();
        }

        public static AsyncManager GetInstance() {
            return instance;
        }

        public void AddJob(Action<Action<Action>> act) {
            jobs.Add(act);
        }
    }
}