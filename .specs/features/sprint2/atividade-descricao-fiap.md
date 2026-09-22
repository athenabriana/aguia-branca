# Challenge - Grupo Águia Branca - Sprint2

## Introdução

Prepare-se: a segunda etapa do desafio vai muito além do app!
Na Sprint 2, vocês vão deixar de lado os mocks e construir o backend real que dará vida ao aplicativo desenvolvido no primeiro
semestre. Isso significa:
- APIs completas;
- integração com o app nativo;
- restrições por nível de acesso;
- consumo de serviços externos;
- integração com IA.
Em resumo: vocês irão transformar o protótipo em uma plataforma robusta de inovação, pronta para
otimizar a gestão do conhecimento e dos projetos da Águia Branca.

Requisitos da entrega da Sprint2

Linguagem e frameworks – Backend:
- 1. Java: Spring Boot, Spring Security, JPA/Hibernate
OU
2. C#: .NET 8 Web API, ASP.NET Identity/JWT, EF Core.
Banco de Dados:
- MongoDB ou outro banco NoSQL.
Integração:
- o aplicativo desenvolvido na Sprint 1 deverá ser integrado com o backend que será construído na
Sprint 2 (sem mocks), utilizando as APIs funcionais para cada um dos recursos e funcionalidades
disponibilizados no App.
Inovação: (Plus)
- uso de IA integrada no filtro/análise das iniciativas ou resultados.

FUNCIONALIDADES - Backend

Autenticação:
- login de 3 perfis de usuários: operadores, gestores e líderes;
- utilizar mecanismos de segurança (JWT, criptografia, entre outros);
- roles: cada operação poderá ser executada de acordo com o nível de acesso do usuário.
Orientações sobre a estratégia da empresa:
- liderança terá acesso para gerenciar as orientações estratégicas para todo o time (CRUD);
- demais perfis poderão apenas consultar as orientações estratégicas;
- registro histórico das estratégias (id, data, categoria, campanha).
Ideias de inovação/Problemas enfrentados pelos operadores:
- operadores poderão cadastrar e consultar suas ideias registradas no aplicativo (CRUD);
- gestores poderão consultar, priorizar e aprovar as melhores ideias;
- vincular as ideias propostas com a estratégia vigente.
Projetos e iniciativas:
- gestores poderão cadastrar os projetos/iniciativas que serão implementadas e, além disso, deverão atualizar os dados dos projetos,
acompanhando seu progresso e adicionando os resultados obtidos (CRUD);
- líderes poderão consultar o andamento dos projetos, verificando dados como etapa, status, investimento, prazo, retorno financeiro;
- vincular os projetos com a estratégia vigente.

FUNCIONALIDADES - Backend

Dashboard:
- líderes poderão consultar um resumo estruturado com os resultados dos projetos, podendo visualizar os retornos específicos por
estratégia ou projeto, e um resumo geral (ROI, lucro obtido, prazo, investimento, aumento de produtividade, entre outros) –
utilização de gráficos e exibições visuais a partir dos endpoints de relatórios retornando com os dados resumidos.
Diferencial – IA (Plus):
- implementem pelo menos uma das opções abaixo de forma efetiva e funcional, utilizando APIs gratuitas (Google Gemini API,
Open Router, Github Models, entre outros):
• integração com IA para pontuação e priorização das iniciativas/ideias de inovação dos colaboradores, auxiliando na seleção
dos futuros projetos - automatização;
• uso de IA generativa como um assistente aos gestores que forem selecionar as melhores ideias e cadastrar os projetos – chat
integrado com IA;
• integração com IA para gerar insights sobre os resultados exibidos nos dashboards, fornecendo análises mais detalhadas e
sugestões de melhorias para a liderança da empresa.

## ENTREGÁVEIS

Entregas obrigatórias
o - Projeto com código fonte do backend zipado (.zip):
• - organização clara por camadas/módulos;
- arquivo read.me contendo instruções de execução.
o - Projeto do aplicativo zipado (.zip):
• - submeter uma pasta compactada contendo todos os
artefatos necessários para o funcionamento do aplicativo;
- aplicativo integrado ao backend (consumindo APIs Rest);
- arquivo APK (Android) ou arquivo IPA (iOS).
o - Apresentação (.PDF ou .PPT):
• - nome e RM dos integrantes;
- diagrama de arquitetura do backend;
o - especificação dos endpoints (rota, método, payload,
resposta).
o * Explicar o modelo de IA utilizada e a funcionalidade
escolhida. (Plus)

Critérios de avaliação
o Implementação técnica funcional — 50%.
o Qualidade do código e boas práticas — 10%.
o Integração do aplicativo com o backend — 15%
o
o Apresentação e documentação — 15%.
o Criatividade e inovação — 10%.