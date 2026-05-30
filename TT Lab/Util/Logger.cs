using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TT_Lab.Util
{
    public class Logger : ILog
    {
        private readonly string typeName;
        private List<string> filterString = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="Logger"/> class.
        /// </summary>
        /// <param name="type">The type.</param>
        public Logger(Type type)
        {
            typeName = type.FullName!;
        }

        public Logger(Type type, List<string> filters)
        {
            typeName = type.FullName!;
            filterString = filters;
        }

        public void AddFilter(string filter)
        {
            filterString.Add(filter);
        }

        /// <summary>
        /// Logs the message as info.
        /// </summary>
        /// <param name="format">A formatted message.</param>
        /// <param name="args">Parameters to be injected into the formatted message.</param>
        public void Info(string format, params object[] args)
        {
            var formatted = string.Format(format, args);
            foreach (var filter in filterString)
            {
                if (formatted.Contains(filter))
                {
                    return;
                }
            }
            Console.WriteLine("[{1}] INFO: {0}", formatted, typeName);
        }

        /// <summary>
        /// Logs the message as a warning.
        /// </summary>
        /// <param name="format">A formatted message.</param>
        /// <param name="args">Parameters to be injected into the formatted message.</param>
        public void Warn(string format, params object[] args)
        {
            var formatted = string.Format(format, args);
            foreach (var filter in filterString)
            {
                if (formatted.Contains(filter))
                {
                    return;
                }
            }
            Console.WriteLine("[{1}] WARN: {0}", formatted, typeName);
        }

        /// <summary>
        /// Logs the exception.
        /// </summary>
        /// <param name="exception">The exception.</param>
        public void Error(Exception exception)
        {
            Console.WriteLine("[{1}] ERROR: {0}", exception, typeName);
        }
    }
}
