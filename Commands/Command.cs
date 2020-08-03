using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AniDBCore.Utils;

namespace AniDBCore.Commands {
    public abstract class Command : ICommand {
        // Properties
        protected readonly Dictionary<string, string> Parameters = new Dictionary<string, string>();
        public readonly string Tag = StaticUtils.GenerateTag();
        public readonly Type ResultType;
        public readonly string CommandBase;

        protected Command(string commandBase, Type resultType) {
            ResultType = resultType.IsSubclassOf(typeof(CommandResult)) == false
                ? throw new Exception("ResultType must inherit from CommandResult")
                : resultType;

            CommandBase = commandBase;
            Parameters.Add("tag", Tag);
        }

        // Methods
        /// <summary>
        /// Gets a list of the parameters to be sent to the API with this command
        /// </summary>
        /// <returns></returns>
        internal string GetParameters() {
            string parameters = string.Empty;

            for (int i = 0; i < Parameters.Count; i++) {
                KeyValuePair<string, string> pair = Parameters.ElementAt(i);

                if (i + 1 != Parameters.Count)
                    parameters += $"{pair.Key.EncodeContent()}={pair.Value.EncodeContent()}&";
                else
                    parameters += $"{pair.Key.EncodeContent()}={pair.Value.EncodeContent()}";
            }

            return parameters;
        }

        public abstract Task<ICommandResult> Send();

        /// <summary>
        /// Sets a parameter to be sent to the API with this command
        /// </summary>
        /// <param name="name">Name of the parameter to set</param>
        /// <param name="value">Value to set the parameter to</param>
        /// <returns>If the parameter value was set</returns>
        public bool SetParameter(string name, string value) {
            try {
                Parameters.Add(name, value);
                return true;
            } catch (ArgumentException) {
                Parameters[name] = value;
                return true;
            } catch (Exception) {
                return false;
            }
        }
    }
}