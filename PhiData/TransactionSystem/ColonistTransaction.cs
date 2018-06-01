﻿using PhiClient.Legacy;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace PhiClient.TransactionSystem
{
    [Serializable]
    public class ColonistTransaction : Transaction
    {
        [NonSerialized]
        public Pawn pawn; // Only sender-side
        public RealmPawn realmPawn;

        public ColonistTransaction(int id, User sender, User receiver, Pawn pawn, RealmPawn realmPawn) : base(id, sender, receiver)
        {
            this.pawn = pawn;
            this.realmPawn = realmPawn;
        }

        public override void OnStartReceiver(RealmData realmData)
        {
            // Double check to ensure it wasn't bypassed by the sender
            if (!receiver.preferences.receiveColonists)
            {
                realmData.NotifyPacketToServer(new ConfirmServerTransactionPacket
                {
                    transaction = this,
                    response = TransactionResponse.DECLINED
                });
                return;
            }

            // We ask for confirmation

            Dialog_GeneralChoice choiceDialog = new Dialog_GeneralChoice(new DialogChoiceConfig
            {
                text = sender.name + " wants to send you a colonist",
                buttonAText = "Accept",
                buttonAAction = () =>
                {
                    realmData.NotifyPacketToServer(new ConfirmServerTransactionPacket
                    {
                        transaction = this,
                        response = TransactionResponse.ACCEPTED
                    });
                },
                buttonBText = "Refuse",
                buttonBAction = () =>
                {
                    realmData.NotifyPacketToServer(new ConfirmServerTransactionPacket
                    {
                        transaction = this,
                        response = TransactionResponse.DECLINED
                    });
                }
            });

            Find.WindowStack.Add(choiceDialog);
        }

        public override void OnEndReceiver(RealmData realmData)
        {
            // Double check to ensure it wasn't bypassed by the sender
            if (!receiver.preferences.receiveItems)
            {
                state = TransactionResponse.DECLINED;
            }

            // Nothing
            if (state == TransactionResponse.ACCEPTED)
            {
                Pawn pawn = realmPawn.FromRealmPawn(realmData);
                
                // We drop it
                IntVec3 position = DropCellFinder.RandomDropSpot(Find.VisibleMap);
                DropPodUtility.MakeDropPodAt(position, Find.VisibleMap, new ActiveDropPodInfo
                {
                    SingleContainedThing = pawn,
                    openDelay = 110,
                    leaveSlag = false
                });

                Find.LetterStack.ReceiveLetter(
                    "Colonist pod",
                    "A colonist was sent to you by " + sender.name,
                    LetterDefOf.PositiveEvent,
                    new RimWorld.Planet.GlobalTargetInfo(position, Find.VisibleMap)
                );
            }
            else if (state == TransactionResponse.INTERRUPTED)
            {
                Messages.Message("Unexpected interruption during item transaction with " + sender.name, MessageTypeDefOf.RejectInput);
            }
            else if (state == TransactionResponse.INTERCEPTED)
            {
                // This should never happen as the server rejects intercepted packets.
            }
        }

        public override void OnEndSender(RealmData realmData)
        {
            // Double check to ensure it wasn't bypassed by the sender
            if (!receiver.preferences.receiveItems)
            {
                state = TransactionResponse.DECLINED;
            }

            if (state == TransactionResponse.ACCEPTED)
            {
                pawn.Destroy();
                Messages.Message(receiver.name + " accepted your items", MessageTypeDefOf.NeutralEvent);
            }
            else if (state == TransactionResponse.DECLINED)
            {
                Messages.Message(receiver.name + " declined your items", MessageTypeDefOf.RejectInput);
            }
            else if (state == TransactionResponse.INTERRUPTED)
            {
                Messages.Message("Unexpected interruption during item transaction with " + receiver.name, MessageTypeDefOf.RejectInput);
            }
            else if (state == TransactionResponse.INTERCEPTED)
            {
                Messages.Message("Transaction with " + receiver.name + " was declined by the server. Are you sending colonists too quickly?", MessageTypeDefOf.RejectInput);
            }
        }
    }
}
