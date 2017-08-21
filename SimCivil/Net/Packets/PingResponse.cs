﻿namespace SimCivil.Net.Packets
{
    /// <summary>
    /// A Ping packet's response.
    /// </summary>
    public class PingResponse : ResponsePacket
    {
        /// <summary>
        /// Construct a new PingResponse Packet.
        /// </summary>
        /// <param name="client">Client to response.</param>
        /// <param name="refpacketID">Ping packet's id.</param>
        public PingResponse(ServerClient client, int refpacketID) : base(client, refpacketID)
        {
        }

        /// <summary>
        /// Called when received and request client object to update timeout flag.
        /// </summary>
        public override void Handle()
        {
            // @panyz522 here is just a example for request/response 
            Client.Update();
        }
    }
}