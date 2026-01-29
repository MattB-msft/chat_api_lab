using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.State;

namespace AgentOrchestrator.Plugins
{
    /// <summary>
    /// Provides a unified context object for agent plugins, bundling the current turn context,
    /// turn state, user authorization, and the name of the authentication handler in use.
    /// </summary>
    public class AgentContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AgentContext"/> class.
        /// </summary>
        /// <param name="context">The current <see cref="ITurnContext"/> for the agent turn.</param>
        /// <param name="turnState">The mutable <see cref="ITurnState"/> associated with the turn.</param>
        /// <param name="authorization">The <see cref="UserAuthorization"/> describing the user's auth context.</param>
        /// <param name="authHandlerName">The name of the auth handler that produced the authorization.</param>
        public AgentContext(ITurnContext context, ITurnState turnState, UserAuthorization authorization, string authHandlerName)
        {
            Context = context;
            State = turnState;
            UserAuth = authorization;
            AuthHandlerName = authHandlerName;
        }

        /// <summary>
        /// Gets the current turn context providing access to activity, services, and responses.
        /// </summary>
        public ITurnContext Context { get; private set; }

        /// <summary>
        /// Gets the turn-scoped state used by plugins to share data across the processing pipeline.
        /// </summary>
        public ITurnState State { get; private set; }

        /// <summary>
        /// Gets the user's authorization information for the current request.
        /// </summary>
        public UserAuthorization UserAuth { get; private set; }

        /// <summary>
        /// Gets the name of the authentication handler used to produce the <see cref="UserAuthorization"/>.
        /// </summary>
        public string AuthHandlerName { get; private set; }

    }
}
