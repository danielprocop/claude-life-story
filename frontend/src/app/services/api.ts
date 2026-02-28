import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface EntryResponse {
  id: string;
  content: string;
  createdAt: string;
  updatedAt: string | null;
  concepts: ConceptResponse[] | null;
}

export interface RelatedEntryResponse {
  id: string;
  contentPreview: string;
  createdAt: string;
  sharedConceptCount: number;
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

export interface ChatResponse {
  answer: string;
  sources: ChatSourceEntry[];
}

export interface ChatSourceEntry {
  entryId: string;
  preview: string;
  date: string;
  similarity: number;
}

export interface ChatHistoryItem {
  role: string;
  content: string;
  createdAt: string;
}

export interface EnergyTrendResponse {
  dataPoints: EnergyDataPoint[];
  avgEnergy: number;
  avgStress: number;
  topEmotions: string[];
  correlations: EnergyCorrelation[];
}

export interface EnergyDataPoint {
  date: string;
  energy: number;
  stress: number;
  emotion: string | null;
}

export interface EnergyCorrelation {
  factor: string;
  effect: string;
  confidence: number;
}

export interface GoalItemResponse {
  id: string;
  title: string;
  description: string | null;
  status: string;
  createdAt: string;
  completedAt: string | null;
  parentGoalId: string | null;
  subGoals: GoalItemResponse[];
}

export interface DashboardResponse {
  stats: DashboardStats;
  energyTrend: EnergyDataPoint[];
  topConcepts: ConceptResponse[];
  activeGoals: GoalItemResponse[];
  recentInsights: InsightResponse[];
  recentEntries: EntryListResponse[];
}

export interface DashboardStats {
  totalEntries: number;
  totalConcepts: number;
  activeGoals: number;
  insightsGenerated: number;
  avgEnergy: number;
  avgStress: number;
}

export interface ReviewResponse {
  summary: string;
  period: string;
  keyThemes: string[];
  accomplishments: string[];
  challenges: string[];
  patterns: string[];
  suggestions: string[];
  generatedAt: string;
  sources: ReviewSourceEntry[];
}

export interface ReviewSourceEntry {
  entryId: string;
  createdAt: string;
  preview: string;
}

export interface SearchResponse {
  query: string;
  entries: EntrySearchHit[];
  concepts: ConceptSearchHit[];
  goalItems: GoalItemSearchHit[];
}

export interface EntrySearchHit {
  id: string;
  preview: string;
  createdAt: string;
  conceptCount: number;
}

export interface ConceptSearchHit {
  id: string;
  label: string;
  type: string;
  entryCount: number;
  lastSeenAt: string;
}

export interface GoalItemSearchHit {
  id: string;
  title: string;
  description: string | null;
  status: string;
  createdAt: string;
  subGoalCount: number;
}

@Injectable({
  providedIn: 'root',
})
export class Api {
  private readonly baseUrl = environment.apiUrl;

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

  getRelatedEntries(id: string, limit = 6): Observable<RelatedEntryResponse[]> {
    return this.http.get<RelatedEntryResponse[]>(`${this.baseUrl}/entries/${id}/related?limit=${limit}`);
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

  sendChatMessage(message: string): Observable<ChatResponse> {
    return this.http.post<ChatResponse>(`${this.baseUrl}/chat`, { message });
  }

  getChatHistory(): Observable<ChatHistoryItem[]> {
    return this.http.get<ChatHistoryItem[]>(`${this.baseUrl}/chat/history`);
  }

  getDashboard(): Observable<DashboardResponse> {
    return this.http.get<DashboardResponse>(`${this.baseUrl}/dashboard`);
  }

  getEnergyTrend(days = 30): Observable<EnergyTrendResponse> {
    return this.http.get<EnergyTrendResponse>(`${this.baseUrl}/energy?days=${days}`);
  }

  getReview(period: string): Observable<ReviewResponse> {
    return this.http.get<ReviewResponse>(`${this.baseUrl}/review/${period}`);
  }

  getGoalItems(): Observable<GoalItemResponse[]> {
    return this.http.get<GoalItemResponse[]>(`${this.baseUrl}/goalitems`);
  }

  createGoalItem(title: string, description?: string, parentGoalId?: string): Observable<GoalItemResponse> {
    return this.http.post<GoalItemResponse>(`${this.baseUrl}/goalitems`, { title, description, parentGoalId });
  }

  updateGoalItem(id: string, title?: string, status?: string): Observable<GoalItemResponse> {
    return this.http.put<GoalItemResponse>(`${this.baseUrl}/goalitems/${id}`, { title, status });
  }

  deleteGoalItem(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/goalitems/${id}`);
  }

  getPatterns(days = 30): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/patterns?days=${days}`);
  }

  search(query: string, limit = 8): Observable<SearchResponse> {
    return this.http.get<SearchResponse>(
      `${this.baseUrl}/search?q=${encodeURIComponent(query)}&limit=${limit}`
    );
  }
}
