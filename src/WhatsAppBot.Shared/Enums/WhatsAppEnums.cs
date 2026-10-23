namespace WhatsAppBot.Shared.Enums;

public enum ConnectionState { NotConnected, Configuring, Connected, Error }
public enum BotReplyMode { FaqOnly, FaqThenAi, AiOnly }
public enum ConversationStatus { Open, Closed }
public enum MessageDirection { Inbound, Outbound }
public enum MessageType { Text, Audio, Image, Unsupported }
public enum ReplySource { FrequentResponse, AI, Admin }
public enum ProcessingState { Received, Processing, Completed, Failed, NoReply }
public enum DeliveryState { Pending, Sent, Delivered, Read, Failed }
public enum MediaKind { Audio, Image }
public enum MediaAvailabilityState { Pending, Available, Expired, Failed, Deleted }
public enum TranscriptState { Pending, Completed, Failed }
public enum WebhookInboxState { Received, Processing, Completed, Failed }
public enum MessageOutboxState { Pending, Sending, Sent, Failed }
public enum WhatsAppOnboardingType { Standard, Coexistence }
public enum AttentionMode { Bot, Humano }
public enum LifecycleStatus { Abierta, Cerrada }
public enum ActorType { Customer, Bot, Admin, System }
public enum Origin { MetaCloudApi, AdminPanel, System, FutureWhatsAppBusinessApp }
