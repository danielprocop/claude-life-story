import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface EntryResponse {
  id: string;
  content: string;
  createdAt: string;
  updatedAt: string | null;
  concepts: ConceptResponse[] | null;
}

export interface EntryListResponse {
  id: string;
  contentPreview: string;
  createdAt: string;
  conceptCount: number;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ConceptResponse {
  id: string;
  label: string;
  type: string;
  firstSeenAt: string;
  lastSeenAt: string;
  entryCount: number;
}

export interface GraphResponse {
  nodes: GraphNode[];
  edges: GraphEdge[];
}

export interface GraphNode {
  id: string;
  label: string;
  type: string;
  weight: number;
}

export interface GraphEdge {
  sourceId: string;
  targetId: string;
  strength: number;
  type: string;
}

export interface InsightResponse {
  id: string;
  content: string;
  generatedAt: string;
  type: string;
}

export interface GoalResponse {
  id: string;
  label: string;
  status: string;
  firstSeenAt: string;
  achievedAt: string | null;
  timeline: GoalTimelineEntry[];
}

export interface GoalTimelineEntry {
  date: string;
  entryPreview: string;
  signal: string;
}

@Injectable({
  providedIn: 'root',
})
export class Api {
  private readonly baseUrl = 'http://localhost:5100/api';

  constructor(private http: HttpClient) {}

  createEntry(content: string): Observable<EntryResponse> {
    return this.http.post<EntryResponse>(`${this.baseUrl}/entries`, { content });
  }

  getEntries(page = 1, pageSize = 20): Observable<PaginatedResponse<EntryListResponse>> {
    return this.http.get<PaginatedResponse<EntryListResponse>>(
      `${this.baseUrl}/entries?page=${page}&pageSize=${pageSize}`
    );
  }

  getEntry(id: string): Observable<EntryResponse> {
    return this.http.get<EntryResponse>(`${this.baseUrl}/entries/${id}`);
  }

  getConcepts(): Observable<ConceptResponse[]> {
    return this.http.get<ConceptResponse[]>(`${this.baseUrl}/concepts`);
  }

  getGraph(): Observable<GraphResponse> {
    return this.http.get<GraphResponse>(`${this.baseUrl}/connections`);
  }

  getInsights(): Observable<InsightResponse[]> {
    return this.http.get<InsightResponse[]>(`${this.baseUrl}/insights`);
  }

  getGoals(): Observable<GoalResponse[]> {
    return this.http.get<GoalResponse[]>(`${this.baseUrl}/goals`);
  }
}
