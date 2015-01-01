﻿using Database.Common;
using Database.Common.Messages;
using Database.Query;
using Database.Storage;
using System.Collections.Generic;
using System.Threading;

namespace Database.Controller
{
    public class ControllerNode : Node
    {
        private bool _primary = false;
        private NodeDefinition _self;
        private ControllerNodeSettings _settings;

        public ControllerNode(ControllerNodeSettings settings)
            : base(settings.Port)
        {
            _settings = settings;
        }

        public override NodeDefinition Self
        {
            get { return _self; }
        }

        public override void Run()
        {
            BeforeStart();

            List<NodeDefinition> controllerNodes = NodeDefinition.ParseConnectionString(_settings.ConnectionString);

            // Find yourself
            _self = null;
            foreach (var def in controllerNodes)
            {
                if (def.IsSelf(_settings.Port))
                {
                    _self = def;
                    break;
                }
            }

            if (_self == null)
            {
                Logger.Log("Could not find myself in the connection string.");
                AfterStop();
                return;
            }

            foreach (var def in controllerNodes)
            {
                if (def != _self)
                {
                    Message message = new Message(def, new JoinAttempt(NodeType.Controller, _self.Hostname, _self.Port, _settings.ToString()), true);
                    message.SendWithoutConfirmation = true;
                    SendMessage(message);
                    message.BlockUntilDone();

                    if (message.Success)
                    {
                        if (message.Response.Data is JoinFailure)
                        {
                            Logger.Log("Failed to join other controllers: " +
                                       ((JoinFailure)message.Response.Data).Reason);
                            AfterStop();
                            return;
                        }
                        else
                        {
                            // success
                            Connections[def].ConnectionEstablished(NodeType.Controller);
                        }
                    }
                }
            }

            if (controllerNodes.Count == 1)
            {
                _primary = true;
            }

            while (Running)
            {
                Thread.Sleep(1);
            }

            AfterStop();
        }

        protected override void MessageReceived(Message message)
        {
            if (message.Data is JoinAttempt)
            {
                JoinAttempt joinAttemptData = (JoinAttempt)message.Data;
                switch (joinAttemptData.Type)
                {
                    case NodeType.Controller:
                        ControllerNodeSettings joinSettings = new ControllerNodeSettings(joinAttemptData.Settings);
                        if (joinSettings.ConnectionString != _settings.ConnectionString)
                        {
                            SendMessage(new Message(message, new JoinFailure("Connection strings do not match."), false));
                        }
                        else if (joinSettings.MaxChunkItemCount != _settings.MaxChunkItemCount)
                        {
                            SendMessage(new Message(message, new JoinFailure("Max chunk item counts do not match."), false));
                        }
                        else if (joinSettings.MaxChunkSize != _settings.MaxChunkSize)
                        {
                            SendMessage(new Message(message, new JoinFailure("Max chunk sizes do not match."), false));
                        }
                        else if (joinSettings.RedundentNodesPerLocation != _settings.RedundentNodesPerLocation)
                        {
                            SendMessage(new Message(message,
                                new JoinFailure("Redundent nodes per location do not match."), false));
                        }
                        else
                        {
                            NodeDefinition nodeDef = new NodeDefinition(joinAttemptData.Name, joinAttemptData.Port);
                            RenameConnection(message.Address, nodeDef);
                            Connections[nodeDef].ConnectionEstablished(joinAttemptData.Type);
                            Message response = new Message(message, new JoinSuccess(_primary), false);
                            response.Address = nodeDef;
                            SendMessage(response);
                        }

                        break;

                    case NodeType.Query:
                        QueryNodeSettings queryJoinSettings = new QueryNodeSettings(joinAttemptData.Settings);
                        if (queryJoinSettings.ConnectionString != _settings.ConnectionString)
                        {
                            SendMessage(new Message(message, new JoinFailure("Connection strings do not match."), false));
                        }
                        else
                        {
                            NodeDefinition nodeDef = new NodeDefinition(joinAttemptData.Name, joinAttemptData.Port);
                            RenameConnection(message.Address, nodeDef);
                            Connections[nodeDef].ConnectionEstablished(joinAttemptData.Type);
                            Message response = new Message(message, new JoinSuccess(_primary), false);
                            response.Address = nodeDef;
                            SendMessage(response);
                        }
                        break;

                    case NodeType.Storage:
                        StorageNodeSettings storageJoinSettings = new StorageNodeSettings(joinAttemptData.Settings);
                        if (storageJoinSettings.ConnectionString != _settings.ConnectionString)
                        {
                            SendMessage(new Message(message, new JoinFailure("Connection strings do not match."), false));
                        }
                        else
                        {
                            NodeDefinition nodeDef = new NodeDefinition(joinAttemptData.Name, joinAttemptData.Port);
                            RenameConnection(message.Address, nodeDef);
                            Connections[nodeDef].ConnectionEstablished(joinAttemptData.Type);
                            Message response = new Message(message, new JoinSuccess(_primary), false);
                            response.Address = nodeDef;
                            SendMessage(response);
                        }

                        break;
                }
            }
        }
    }
}