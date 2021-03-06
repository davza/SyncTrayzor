﻿using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SyncTrayzor.Services
{
    public interface IAutostartProvider
    {
        bool IsEnabled { get; set; }
        bool CanRead { get; }
        bool CanWrite { get; }

        void UpdatePathToSelf();
        AutostartConfiguration GetCurrentSetup();
        void SetAutoStart(AutostartConfiguration config);
    }

    public class AutostartConfiguration
    {
        public bool AutoStart { get; set; }
        public bool StartMinimized { get; set; }

        public override string ToString()
        {
            return String.Format("<AutostartConfiguration AutoStart={0} StartMinimized={1}>", this.AutoStart, this.StartMinimized);
        }
    }

    public class AutostartProvider : IAutostartProvider
    {
        private const string applicationName = "SyncTrayzor";
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        public bool IsEnabled { get; set; }

        private bool _canRead;
        public bool CanRead
        {
            get { return this.IsEnabled && this._canRead; }
        }

        private bool _canWrite;
        public bool CanWrite
        {
            get { return this.IsEnabled && this._canWrite; }
        }

        public AutostartProvider()
        {
            // Default
            this.IsEnabled = true;

            // Check our access
            try
            {
                this.OpenRegistryKey(true).Dispose();
                this._canWrite = true;
                this._canRead = true;
                logger.Info("Have read/write access to the registry");
                return;
            }
            catch (SecurityException) { }

            try
            {
                this.OpenRegistryKey(false).Dispose();
                this._canRead = true;
                logger.Info("Have read-only access to the registry");
                return;
            }
            catch (SecurityException) { }

            logger.Info("Have no access to the registry");
        }

        private RegistryKey OpenRegistryKey(bool writable)
        {
            return Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable);
        }

        public void UpdatePathToSelf()
        {
            if (!this.CanWrite)
                throw new InvalidOperationException("Don't have permission to write to the registry");

            var config = this.GetCurrentSetup();
            this.SetAutoStart(config);
        }

        public AutostartConfiguration GetCurrentSetup()
        {
            if (!this.CanRead)
                throw new InvalidOperationException("Don't have permission to read the registry");

            bool autoStart = false;
            bool startMinimized = false;

            using (var registryKey = this.OpenRegistryKey(false))
            {
                var value = registryKey.GetValue(applicationName) as string;
                if (value != null)
                {
                    autoStart = true;
                    if (value.Contains(" -minimized"))
                        startMinimized = true;
                }
            }

            var config = new AutostartConfiguration() { AutoStart = autoStart, StartMinimized = startMinimized };
            logger.Info("GetCurrentSetup determined that the current configuration is: {0}", config);
            return config;
        }

        public void SetAutoStart(AutostartConfiguration config)
        {
            if (!this.CanWrite)
                throw new InvalidOperationException("Don't have permission to write to the registry");

            logger.Info("Setting AutoStart to {0}", config);

            using (var registryKey = this.OpenRegistryKey(true))
            {
                var keyExists = registryKey.GetValue(applicationName) != null;

                if (config.AutoStart)
                {
                    var path = String.Format("\"{0}\"{1}", Assembly.GetExecutingAssembly().Location, config.StartMinimized ? " -minimized" : "");
                    logger.Debug("Autostart path: {0}", path);
                    registryKey.SetValue(applicationName, path);
                }
                else if (keyExists)
                {
                    logger.Debug("Removing pre-existing registry key");
                    registryKey.DeleteValue(applicationName);
                }
            }
        }
    }
}
