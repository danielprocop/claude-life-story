import { Component, OnInit, signal, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, ChatHistoryItem, ChatSourceEntry } from '../services/api';

@Component({
  selector: 'app-chat',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './chat.html',
  styleUrl: './chat.scss',
})
export class Chat implements OnInit {
  messages = signal<ChatHistoryItem[]>([]);
  input = signal('');
  sending = signal(false);
  sources = signal<ChatSourceEntry[]>([]);
  @ViewChild('messagesContainer') messagesContainer!: ElementRef;

  constructor(private api: Api) {}

  ngOnInit() {
    this.api.getChatHistory().subscribe({
      next: (history) => this.messages.set(history),
    });
  }

  send() {
    const msg = this.input().trim();
    if (!msg || this.sending()) return;

    this.sending.set(true);
    this.messages.update((msgs) => [...msgs, { role: 'user', content: msg, createdAt: new Date().toISOString() }]);
    this.input.set('');
    this.scrollToBottom();

    this.api.sendChatMessage(msg).subscribe({
      next: (res) => {
        this.messages.update((msgs) => [
          ...msgs,
          { role: 'assistant', content: res.answer, createdAt: new Date().toISOString() },
        ]);
        this.sources.set(res.sources);
        this.sending.set(false);
        this.scrollToBottom();
      },
      error: () => {
        this.messages.update((msgs) => [
          ...msgs,
          { role: 'assistant', content: 'Errore nella risposta. Riprova.', createdAt: new Date().toISOString() },
        ]);
        this.sending.set(false);
      },
    });
  }

  onKeydown(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  private scrollToBottom() {
    setTimeout(() => {
      if (this.messagesContainer) {
        const el = this.messagesContainer.nativeElement;
        el.scrollTop = el.scrollHeight;
      }
    }, 50);
  }
}
