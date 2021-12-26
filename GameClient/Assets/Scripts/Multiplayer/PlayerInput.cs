using UnityEngine;

namespace Multiplayer
{
    /// <summary> Processes input, sends input, reconciliates, makes client prediction, controls interpolation of local player </summary>
    public class PlayerInput : MonoBehaviour
    {
        public struct Inputs
        {
            public readonly ushort buttons;

            public Inputs(ushort value) : this() => buttons = value;

            public bool IsUp(ushort button) => IsDown(button) == false;

            public bool IsDown(ushort button) => (buttons & button) == button;

            public static implicit operator Inputs(ushort value) => new Inputs(value);
        }

        static ConvarRef interp = new ConvarRef("interpolation");

        public Player playerManager;
        public PlayerMovement playerMovement;

        public Camera playerCamera;
        public Rigidbody rb;

        [HideInInspector]
        public Vector3 velocity = Vector3.zero;
        public Vector3 angularVelocity = Vector3.zero;

        // The maximum cache size for both the ClientInputState and SimulationState caches.
        private const int STATE_CACHE_SIZE = 1024;

        // The client's current simulation frame. 
        private int simulationFrame;
        // The last simulationFrame that we Reconciled from the server.
        private int lastCorrectedFrame;

        // The cache that stores all of the client's predicted movement reuslts. 
        private SimulationState[] simulationStateCache;
        // The cache that stores all of the client's inputs. 
        private ClientInputState[] inputStateCache;
        // The last known SimulationState provided by the server.
        private SimulationState serverSimulationState;
        // The client's current ClientInputState.
        private ClientInputState inputState;

        private ConsoleUI consoleUI;

        private void Awake()
        {
            rb.freezeRotation = true;
            rb.isKinematic = true;

            lastCorrectedFrame = 0;
            simulationFrame = 0;

            serverSimulationState = new SimulationState();
            simulationStateCache = new SimulationState[STATE_CACHE_SIZE];
            inputStateCache = new ClientInputState[STATE_CACHE_SIZE];
            inputState = new ClientInputState();
        }

        void Start()
        {
            consoleUI = FindObjectOfType<ConsoleUI>();

            //Assign id of local player
            Player.myId = playerManager.id;

            playerMovement.SetTransformAndRigidbody(transform, rb);
        }

        private void FixedUpdate()
        {
            // Process inputs
            ProcessInput(inputState);

            // Reconciliate if there's a message from the server
            if (serverSimulationState != null) Reconciliate();

            // Get current simulationState
            SimulationState simulationState =
                SimulationState.CurrentSimulationState(inputState, this);

            // Determine the cache index based on on modulus operator.
            int cacheIndex = simulationFrame % STATE_CACHE_SIZE;

            // Store the SimulationState into the simulationStateCache 
            simulationStateCache[cacheIndex] = simulationState;

            // Store the ClientInputState into the inputStateCache
            inputStateCache[cacheIndex] = inputState;

            // Send inputs so the server can process them
            SendInputToServer();

            // Move next frame
            ++simulationFrame;

            // Add position to interpolate
            if (playerManager.interpolation.target == Interpolation.InterpolationTarget.localPlayer) playerManager.interpolation.PreviousPosition = rb.position;
            if (playerManager.interpolation.target == Interpolation.InterpolationTarget.localPlayerDeltaSnapshot) playerManager.interpolation.PlayerUpdate(simulationFrame, rb.position);
        }

        private void Update()
        {
            // Console is open, dont move
            if (consoleUI.isActive())
            {
                inputState = new ClientInputState
                {
                    tick = GlobalVariables.clientTick - Utils.timeToTicks(interp.GetValue()),
                    lerpAmount = GlobalVariables.lerpAmount,
                    simulationFrame = simulationFrame,
                    buttons = 0,
                    HorizontalAxis = 0f,
                    VerticalAxis = 0f,
                    rotation = playerCamera.transform.rotation,
                };
                return;
            }

            // Set correspoding buttons
            ushort buttons = 0;
            if (Input.GetButton("Jump"))
                buttons |= Button.Jump;
            if (Input.GetButton("Fire1"))
                buttons |= Button.Fire1;

            // Set new input
            inputState = new ClientInputState
            {
                tick = GlobalVariables.clientTick - Utils.timeToTicks(interp.GetValue()),
                lerpAmount = GlobalVariables.lerpAmount,
                simulationFrame = simulationFrame,
                buttons = buttons,
                HorizontalAxis = Input.GetAxisRaw("Horizontal"),
                VerticalAxis = Input.GetAxisRaw("Vertical"),
                rotation = playerCamera.transform.rotation,
            };

            Vector3 localVelocity = Quaternion.Euler(0, transform.rotation.eulerAngles.y - playerCamera.transform.rotation.eulerAngles.y, 0) * new Vector3(velocity.x, 0, velocity.z);
            //playerManager.playerAnimation.IsFiring(Input.GetButton("Fire1"));
            //playerManager.playerAnimation.UpdateAnimatorProperties(localVelocity.x/moveSpeed.GetValue(), localVelocity.z/moveSpeed.GetValue(), isGrounded, Input.GetButton("Jump"));
        }

        public void ProcessInput(ClientInputState inputs)
        {
            playerMovement.RotationCheck(inputs);

            rb.isKinematic = false;
            rb.velocity = velocity;
            //rb.angularVelocity = velocity;

            playerMovement.CalculateVelocity(inputs);
            Physics.Simulate(LogicTimer.FixedDelta);

            //angularVelocity = rb.velocity;
            velocity = rb.velocity;
            rb.isKinematic = true;
        }

        private void SendInputToServer()
        {
            SendMessages.PlayerInput(inputState);
            // We send all inputs that havent been acked
            for (int frameToSend = lastCorrectedFrame + 1; frameToSend <= serverSimulationState.simulationFrame; frameToSend++)
            {
                // Determine the cache index 
                int cacheIndex = frameToSend % STATE_CACHE_SIZE;

                // Obtain the cached input and simulation states.
                ClientInputState cachedInputState = inputStateCache[cacheIndex];

                if (cachedInputState != null) SendMessages.PlayerInput(cachedInputState);
            }
        }

        private void setPlayerToSimulationState(SimulationState state)
        {
            transform.position = state.position;
            velocity = state.velocity;
        }

        public void Reconciliate()
        {
            // Sanity check, don't reconciliate for old states.
            if (serverSimulationState.simulationFrame <= lastCorrectedFrame) return;

            // Determine the cache index 
            int cacheIndex = serverSimulationState.simulationFrame % STATE_CACHE_SIZE;

            // Obtain the cached input and simulation states.
            ClientInputState cachedInputState = inputStateCache[cacheIndex];
            SimulationState cachedSimulationState = simulationStateCache[cacheIndex];

            // If there's missing cache data for either input or simulation 
            // snap the player's position to match the server.
            if (cachedInputState == null || cachedSimulationState == null)
            {
                setPlayerToSimulationState(serverSimulationState);

                // Set the last corrected frame to equal the server's frame.
                lastCorrectedFrame = serverSimulationState.simulationFrame;
                return;
            }

            // If the simulation time isnt equal to the server time then return
            // this should never happen
            if (cachedInputState.simulationFrame != serverSimulationState.simulationFrame || cachedSimulationState.simulationFrame != serverSimulationState.simulationFrame)
                return;

            // Find the difference between the vector's values. 
            Vector3 difference = cachedSimulationState.position - serverSimulationState.position;

            //  The amount of distance in units that we will allow the client's
            //  prediction to drift from it's position on the server, before a
            //  correction is necessary. 
            float tolerance = 0.1f;

            // A correction is necessary.
            if (difference.sqrMagnitude > tolerance)
            {
                // Show warning about misprediction
                Debug.LogWarning("Client misprediction with a difference of " + difference + " at frame " + serverSimulationState.simulationFrame + ".");
                DebugScreen.mispredictions++;

                // Set the player's position to match the server's state. 
                setPlayerToSimulationState(serverSimulationState);

                // Declare the rewindFrame as we're about to resimulate our cached inputs. 
                int rewindFrame = serverSimulationState.simulationFrame;

                // Loop through and apply cached inputs until we're 
                // caught up to our current simulation frame. 
                while (rewindFrame < simulationFrame)
                {
                    // Determine the cache index 
                    int rewindCacheIndex = rewindFrame % STATE_CACHE_SIZE;

                    // Obtain the cached input and simulation states.
                    ClientInputState rewindCachedInputState = inputStateCache[rewindCacheIndex];
                    SimulationState rewindCachedSimulationState = simulationStateCache[rewindCacheIndex];

                    // If there's no state to simulate, for whatever reason, 
                    // increment the rewindFrame and continue.
                    if (rewindCachedInputState == null || rewindCachedSimulationState == null)
                    {
                        ++rewindFrame;
                        continue;
                    }

                    // Process the cached inputs. 
                    ProcessInput(rewindCachedInputState);
                    //SecondProcessInput(rewindCachedInputState);

                    // Replace the simulationStateCache index with the new value.
                    SimulationState rewoundSimulationState = SimulationState.CurrentSimulationState(rewindCachedInputState, this);
                    rewoundSimulationState.simulationFrame = rewindFrame;
                    simulationStateCache[rewindCacheIndex] = rewoundSimulationState;

                    // Increase the amount of frames that we've rewound.
                    ++rewindFrame;
                }
            }

            // Once we're complete, update the lastCorrectedFrame to match.
            // NOTE: Set this even if there's no correction to be made. 
            lastCorrectedFrame = serverSimulationState.simulationFrame;
        }

        // We received a new simualtion state, overwrite it
        public void OnServerSimulationStateReceived(SimulationState simulationState)
        {
            if (serverSimulationState?.simulationFrame < simulationState.simulationFrame)
                serverSimulationState = simulationState;
        }
    }
}