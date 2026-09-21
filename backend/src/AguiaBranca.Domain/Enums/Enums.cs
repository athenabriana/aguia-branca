namespace AguiaBranca.Domain.Enums;

// Nomes de membros em UPPER_SNAKE de propósito: são exatamente os valores persistidos no Mongo e
// trafegados na API/app (SUBMETIDA, EM_ANALISE...), evitando qualquer camada de tradução.

public enum Role { OPERADOR, GESTOR, LIDER }

public enum Division { PASSAGEIROS, COMERCIO, LOGISTICA, CORPORATIVO }

public enum IdeaStatus { SUBMETIDA, EM_ANALISE, APROVADA, REJEITADA, IMPLEMENTADA }

public enum ProjectStage { PLANEJAMENTO, EM_EXECUCAO, CONCLUIDO, CANCELADO }

public enum Pillar { DIRECIONAMENTO, IDEIAS, PROJETOS, MENSURACAO }

public enum Period { THIS_MONTH, LAST_QUARTER, THIS_YEAR, ALL }

public enum PointReason { IDEA_CREATED, IDEA_DELETED, IDEA_APPROVED, IDEA_IMPLEMENTED, MIGRATION }

public enum GuidelineAction { CREATED, UPDATED, DELETED }

public enum FieldValueKind { TEXT, NUMBER, DATE }
