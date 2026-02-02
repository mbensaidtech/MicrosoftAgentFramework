export interface ChatMessage {
  id: string;
  role: 'customer' | 'seller';
  content: string;
  timestamp: Date;
  isTyping?: boolean;
}

export interface Participant {
  id: string;
  name: string;
  role: 'customer' | 'seller';
  avatar: string;
}
