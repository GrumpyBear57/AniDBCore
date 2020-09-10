using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AniDBCore.Events;
using AniDBCore.Utils;

namespace AniDBCore.Commands {
    public abstract class Command : ICommand {
        // Properties
        public readonly IReadOnlyDictionary<string, DataType> OptionalParameters;
        public readonly CommandTag Tag = new CommandTag();
        public readonly bool RequiresSession;
        public readonly string CommandBase;
        public readonly Type ResultType;

        protected readonly Dictionary<string, string> Parameters = new Dictionary<string, string>();
        protected bool Sent;

        protected Command(string commandBase, bool requiresSession, Type resultType,
                          IReadOnlyDictionary<string, DataType> optionalParameters) {
            ResultType = resultType.IsSubclassOf(typeof(CommandResult)) == false
                ? throw new Exception("ResultType must inherit from CommandResult")
                : resultType;

            CommandBase = commandBase;
            RequiresSession = requiresSession;
            Parameters.Add("tag", Tag.ToString());
            OptionalParameters = optionalParameters;
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

        public virtual async Task<ICommandResult> Send() {
            //TODO add a Duplicate() method (or something similar)
            if (Sent)
                AniDB.InvokeClientError(new ClientErrorArgs("Cannot send a command that has already been sent!"));

            Sent = true;
            return await Client.QueueCommand(this);
        }

        /// <summary>
        /// Sets an optional parameter to be sent to the API with this command
        /// </summary>
        /// <param name="name">Name of the parameter to set</param>
        /// <param name="value">Value to set the parameter to</param>
        /// <param name="error">Error explaining why the operation failed</param>
        /// <returns>If the parameter value was set</returns>
        public bool SetOptionalParameter(string name, string value, out string error) {
            error = string.Empty;
            if (StaticUtils.IsParameterValid(name, value, OptionalParameters, ref error) == false)
                return false;

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