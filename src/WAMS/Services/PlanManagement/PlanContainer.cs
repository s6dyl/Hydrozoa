﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using WAMS.DataModels;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using WAMS.Services.GPIOAccess;

namespace WAMS.Services.PlanManagement
{
    public static class PlanContainer
    {
        private static string BackupPath;

        private static Timer BackupPeriod;

        private static bool FirstTime = true;

        private static ILogger _logger { set; get; }
        private static IValve Valve { get; set; }
        public static List<Plan> Container { get; set; }
        public static DataModels.Action ActiveAction { get; set; }

        public static void Setup(ILoggerFactory loggerFactory, IValve _Valve)
        {
            if (FirstTime) {
                _logger = loggerFactory.CreateLogger("PlanContainer");
                FirstTime = false;
                Valve = _Valve;
                LoadBackup();

                BackupPeriod = new Timer(60000000);
                BackupPeriod.Elapsed += BackupEvent;
                BackupPeriod.AutoReset = true;
                BackupPeriod.Enabled = true;
            }
        }

        private static void BackupEvent(object sender, ElapsedEventArgs k)
        {
            string Backup = JsonConvert.SerializeObject(Container, Formatting.Indented);

            try {
                if (File.Exists(BackupPath)) { File.Delete(BackupPath); }
                using (FileStream Fs = File.Create(BackupPath)) {
                    byte[] data = new UTF8Encoding(true).GetBytes(Backup);
                    Fs.Write(data, 0, data.Length);
                }
            } catch (IOException ex) { _logger.LogCritical(ex.Message); }
        }

        private static void LoadBackup()
        {
            try {
                BackupPath = string.Concat(AppDomain.CurrentDomain.BaseDirectory, "/PlanBackup/file.bkp");
                if (!File.Exists(BackupPath)) { Container = new List<Plan>(); return; } else {
                    using (FileStream Fs = new FileStream(BackupPath, FileMode.Open, FileAccess.Read)) {
                        byte[] data = new byte[Fs.Length];
                        int ToRead = (int)Fs.Length;
                        int Read = 0;
                        while (ToRead > 0) {
                            int modifier = Fs.Read(data, Read, ToRead);
                            if (modifier == 0) { break; }
                            ToRead -= modifier;
                            Read += modifier;
                        }
                        Container = JsonConvert.DeserializeObject<List<Plan>>(Encoding.UTF8.GetString(data));
                    }
                    _logger.LogInformation("Plans were successfully loaded from the backup file !");
                }
            } catch (IOException ex) { _logger.LogCritical(ex.Message); }
        }

        public static bool AddPlan(Plan NewPlan)
        {
            if (Container.All(e => !(e.Name.Equals(NewPlan.Name)))) {
                Container.Add(NewPlan);
                return true;
            } else { return false; }
        }

        public static bool AddAction(DataModels.Action Element)
        {
            if (Container.Any(e => e.Name.Equals(Element.PlanName))) {
                if (Container.Where(e => e.Name.Equals(Element.PlanName)).First().Elements.All(e => !(e.Name.Equals(Element.Name)))) {
                    Container.Where(e => e.Name.Equals(Element.PlanName)).First().Elements.Add(Element);
                    return true;
                }
            }
            return false;
        }

        public static bool RemovePlan(string Name)
        {
            if (!Container.Any(e => e.Name.Equals(Name))) { return false; } else {
                Container.RemoveAll(e => e.Name.Equals(Name));
                if (ActiveAction.PlanName == Name) {
                    ActiveAction = null;
                    Valve.Shut();
                }
                return true;
            }
        }

        public static bool RemoveAction(string PlanName, string Name)
        {
            if (!Container.Any(e => e.Name.Equals(PlanName))) { return false; } else {
                if (!Container.Where(e => e.Name.Equals(PlanName)).First().Elements.Any(e => e.Name.Equals(Name))) { return false; }
                Container.Where(e => e.Name.Equals(PlanName)).First().Elements.RemoveAll(e => e.Name.Equals(Name));
                if (ActiveAction.Name == Name && ActiveAction.PlanName == PlanName) {
                    ActiveAction = null;
                    Valve.Shut();
                }
                return true;
            }
        }
    }
}
