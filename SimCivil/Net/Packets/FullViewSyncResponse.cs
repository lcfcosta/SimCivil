﻿using SimCivil.Map;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace SimCivil.Net.Packets
{
    /// <summary>
    /// Response to a full view sync request.
    /// </summary>
    [PacketType(PacketType.FullViewSyncResponse)]
    public class FullViewSyncResponse : ResponsePacket
    {
        public FullViewSyncResponse((int x,int y) center,int viewDistence,IEnumerable<Tile> tiles)
        {
            Center = center;
            ViewDistence = viewDistence;
            Tiles = tiles;
        }

        public FullViewSyncResponse(PacketType type = PacketType.Empty, Hashtable data = null, IServerConnection client = null)
            : base(type, data, client) { }

        public (int x, int y) Center
        {
            get
            {
                return ((int x, int y))Data[nameof(Center)];
            }
            set
            {
                Data[nameof(Center)] = value;
            }
        }

        public int ViewDistence
        {
            get
            {
                return (int)Data[nameof(ViewDistence)];
            }
            set
            {
                Data[nameof(ViewDistence)] = value;
            }
        }

        public IEnumerable<Tile> Tiles
        {
            get
            {
                return (IEnumerable<Tile>)Data[nameof(Tiles)];
            }
            set
            {
                Data[nameof(Tiles)] = value;
            }
        }
    }
}
