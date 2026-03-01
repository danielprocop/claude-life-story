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

export interface EntryEntityFeedbackResponse {
  entryId: string;
  label: string;
  appliedKind: string;
  rebuildQueued: boolean;
  message: string;
}

export interface EntryListResponse {
  id: string;
  contentPreview: string;
  createdAt: string;
  conceptCount: number;
}

export type TimelineViewMode = 'day' | 'week' | 'month' | 'year';

export interface TimelineEntryCardResponse {
  id: string;
  contentPreview: string;
  createdAt: string;
  conceptCount: number;
}

export interface TimelineBucketResponse {
  bucketKey: string;
  label: string;
  startUtc: string;
  endUtc: string;
  entryCount: number;
  hasMoreEntries: boolean;
  entries: TimelineEntryCardResponse[];
}

export interface EntriesTimelineResponse {
  view: TimelineViewMode;
  bucketCount: number;
  entriesPerBucket: number;
  timezoneOffsetMinutes: number;
  rangeStartUtc: string;
  rangeEndUtc: string;
  currentBucketStartUtc: string;
  hasPrevious: boolean;
  hasNext: boolean;
  previousCursorUtc: string | null;
  nextCursorUtc: string | null;
  buckets: TimelineBucketResponse[];
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

export interface PersonalModelResponse {
  generatedAt: string;
  entriesAnalyzed: number;
  canonicalEntities: number;
  activeGoals: number;
  contextSummary: string;
  personalitySignals: ProfileSignalResponse[];
  philosophicalThemes: string[];
  currentFocus: string[];
  suggestedMicroSteps: string[];
  adaptationRules: string[];
}

export interface ProfileSignalResponse {
  trait: string;
  score: number;
  rationale: string;
}

export interface OpenDebtResponse {
  counterpartyEntityId: string;
  counterpartyName: string;
  amountOpen: number;
  currency: string;
  openItems: number;
}

export interface SpendingSummaryResponse {
  from: string;
  to: string;
  total: number;
  currency: string;
}

export interface EventSpendingSummaryResponse {
  eventType: string;
  from: string;
  to: string;
  totalEventSpend: number;
  eventCount: number;
  currency: string;
}

export interface ClarificationQuestionResponse {
  id: string;
  questionType: string;
  prompt: string;
  createdAt: string;
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
  entities: EntitySearchHit[];
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

export interface EntitySearchHit {
  id: string;
  kind: string;
  canonicalName: string;
  anchorKey: string | null;
  aliases: string[];
  evidenceCount: number;
  updatedAt: string;
  resolutionState: 'normal' | 'ambiguous' | 'suppressed_candidate' | string;
}

export interface NodeSearchResponse {
  query: string;
  items: NodeSearchItemResponse[];
  totalCount: number;
  kindCounts: NodeKindCountResponse[];
}

export interface NodeSearchItemResponse {
  id: string;
  kind: string;
  canonicalName: string;
  anchorKey: string | null;
  aliases: string[];
  evidenceCount: number;
  updatedAt: string;
  resolutionState: 'normal' | 'ambiguous' | 'suppressed_candidate' | string;
}

export interface NodeKindCountResponse {
  kind: string;
  count: number;
}

export interface NodeViewResponse {
  id: string;
  kind: string;
  canonicalName: string;
  anchorKey: string | null;
  aliases: string[];
  relations: NodeRelationResponse[];
  evidence: NodeEvidenceResponse[];
  resolutionNotes: string[];
  person: PersonNodeViewResponse | null;
  event: EventNodeViewResponse | null;
}

export interface NodeRelationResponse {
  type: string;
  target: string;
}

export interface NodeEvidenceResponse {
  entryId: string;
  evidenceType: string;
  snippet: string;
  recordedAt: string;
  mergeReason: string | null;
}

export interface PersonNodeViewResponse {
  openUserOwes: number;
  openOwedToUser: number;
  sharedEvents: PersonEventSummaryResponse[];
  settlements: SettlementSummaryResponse[];
}

export interface PersonEventSummaryResponse {
  eventEntityId: string;
  title: string;
  eventType: string;
  occurredAt: string;
  eventTotal: number | null;
  myShare: number | null;
}

export interface SettlementSummaryResponse {
  settlementId: string;
  direction: string;
  originalAmount: number;
  remainingAmount: number;
  currency: string;
  status: string;
  createdAt: string;
  eventEntityId: string | null;
  eventTitle: string | null;
}

export interface EventNodeViewResponse {
  eventType: string;
  title: string;
  occurredAt: string;
  eventTotal: number | null;
  myShare: number | null;
  currency: string;
  includesUser: boolean;
  participants: EventParticipantResponse[];
  settlements: SettlementSummaryResponse[];
  sourceEntryId: string;
}

export interface EventParticipantResponse {
  entityId: string;
  canonicalName: string;
  anchorKey: string | null;
  role: string;
}

export interface RebuildMemoryResponse {
  queued: boolean;
  userId: string;
}

export interface ReindexEntitiesResponse {
  reindexed: number;
}

export interface NormalizeEntitiesResponse {
  normalized: number;
  merged: number;
  suppressed: number;
  ambiguous: number;
  reindexed: number;
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

  updateEntry(id: string, content: string): Observable<EntryResponse> {
    return this.http.put<EntryResponse>(`${this.baseUrl}/entries/${id}`, { content });
  }

  getEntries(page = 1, pageSize = 20): Observable<PaginatedResponse<EntryListResponse>> {
    return this.http.get<PaginatedResponse<EntryListResponse>>(
      `${this.baseUrl}/entries?page=${page}&pageSize=${pageSize}`
    );
  }

  getEntriesTimeline(
    view: TimelineViewMode,
    cursorUtc?: string,
    bucketCount = 10,
    entriesPerBucket = 8,
    timezoneOffsetMinutes = new Date().getTimezoneOffset()
  ): Observable<EntriesTimelineResponse> {
    const params = [
      `view=${encodeURIComponent(view)}`,
      `bucketCount=${bucketCount}`,
      `entriesPerBucket=${entriesPerBucket}`,
      `timezoneOffsetMinutes=${timezoneOffsetMinutes}`,
    ];

    if (cursorUtc) {
      params.push(`cursorUtc=${encodeURIComponent(cursorUtc)}`);
    }

    return this.http.get<EntriesTimelineResponse>(`${this.baseUrl}/entries/timeline?${params.join('&')}`);
  }

  getEntry(id: string): Observable<EntryResponse> {
    return this.http.get<EntryResponse>(`${this.baseUrl}/entries/${id}`);
  }

  getRelatedEntries(id: string, limit = 6): Observable<RelatedEntryResponse[]> {
    return this.http.get<RelatedEntryResponse[]>(`${this.baseUrl}/entries/${id}/related?limit=${limit}`);
  }

  submitEntryEntityFeedback(
    id: string,
    label: string,
    expectedKind: string,
    note?: string
  ): Observable<EntryEntityFeedbackResponse> {
    return this.http.post<EntryEntityFeedbackResponse>(`${this.baseUrl}/entries/${id}/feedback/entity`, {
      label,
      expectedKind,
      note,
    });
  }

  deleteEntry(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/entries/${id}`);
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

  getProfile(): Observable<PersonalModelResponse> {
    return this.http.get<PersonalModelResponse>(`${this.baseUrl}/profile`);
  }

  getProfileQuestions(limit = 5): Observable<ClarificationQuestionResponse[]> {
    return this.http.get<ClarificationQuestionResponse[]>(`${this.baseUrl}/profile/questions?limit=${limit}`);
  }

  answerProfileQuestion(questionId: string, answer: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/profile/questions/${questionId}/answer`, { answer });
  }

  getOpenDebts(): Observable<OpenDebtResponse[]> {
    return this.http.get<OpenDebtResponse[]>(`${this.baseUrl}/ledger/debts`);
  }

  getDebtForCounterparty(counterparty: string): Observable<OpenDebtResponse> {
    return this.http.get<OpenDebtResponse>(`${this.baseUrl}/ledger/debts/${encodeURIComponent(counterparty)}`);
  }

  getMySpending(from?: string, to?: string): Observable<SpendingSummaryResponse> {
    const params: string[] = [];
    if (from) params.push(`from=${encodeURIComponent(from)}`);
    if (to) params.push(`to=${encodeURIComponent(to)}`);
    const qs = params.length ? `?${params.join('&')}` : '';
    return this.http.get<SpendingSummaryResponse>(`${this.baseUrl}/ledger/spending/my${qs}`);
  }

  getEventSpending(eventType: string, from?: string, to?: string): Observable<EventSpendingSummaryResponse> {
    const params = [`eventType=${encodeURIComponent(eventType)}`];
    if (from) params.push(`from=${encodeURIComponent(from)}`);
    if (to) params.push(`to=${encodeURIComponent(to)}`);
    return this.http.get<EventSpendingSummaryResponse>(`${this.baseUrl}/ledger/spending/events?${params.join('&')}`);
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

  getNodes(query = '', limit = 24): Observable<NodeSearchResponse> {
    const encodedQuery = encodeURIComponent(query);
    return this.http.get<NodeSearchResponse>(`${this.baseUrl}/nodes?q=${encodedQuery}&limit=${limit}`);
  }

  getNode(id: string): Observable<NodeViewResponse> {
    return this.http.get<NodeViewResponse>(`${this.baseUrl}/nodes/${id}`);
  }

  rebuildMemory(): Observable<RebuildMemoryResponse> {
    return this.http.post<RebuildMemoryResponse>(`${this.baseUrl}/operations/rebuild/memory`, {});
  }

  reindexEntities(): Observable<ReindexEntitiesResponse> {
    return this.http.post<ReindexEntitiesResponse>(`${this.baseUrl}/operations/reindex/entities`, {});
  }

  normalizeEntities(): Observable<NormalizeEntitiesResponse> {
    return this.http.post<NormalizeEntitiesResponse>(`${this.baseUrl}/operations/normalize/entities`, {});
  }
}
