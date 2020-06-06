using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AniDBCore.Commands {
    public abstract class Command : ICommand {
        // Properties
        protected readonly Dictionary<string, string> Parameters = new Dictionary<string, string>();
        public readonly string Tag = Guid.NewGuid().ToString();
        public readonly Type ResultType;

        protected Command(Type resultType) {
            ResultType = resultType != typeof(CommandResult)
                ? throw new Exception("ResultType must inherit from CommandResult")
                : resultType;
        }

        // Methods
        /// <summary>
        /// Gets a list of the parameters to be sent to the API with this command
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<KeyValuePair<string, string>> GetParameters() {
            return Parameters.ToList();
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