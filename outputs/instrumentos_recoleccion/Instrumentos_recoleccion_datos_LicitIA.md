# Instrumentos de recolección de datos

**Proyecto:** LicitIA - Plataforma web inteligente para la gestión de convocatorias estatales

## 1. Instrumentos construidos

Para la investigación se construyen tres instrumentos aplicables: la ficha de registro del pretest, la ficha de registro del postest y la matriz de consolidación y comparación de indicadores. Estos instrumentos permiten registrar datos de la misma muestra de 30 convocatorias estatales antes y después de la implementación de la plataforma web inteligente.

## 2. Ficha de registro del pretest: proceso manual

**Nombre del instrumento:** Ficha de registro del pretest para el proceso manual de búsqueda y monitoreo de convocatorias.

**Finalidad:** Registrar los datos obtenidos antes de la implementación de la plataforma web inteligente, a fin de medir la situación inicial del proceso tradicional de búsqueda, monitoreo y registro de convocatorias estatales.

**Unidad de análisis:** Convocatoria estatal revisada manualmente en el portal SEACE.

**Aplicación:** Sobre una muestra de 30 convocatorias filtradas según el rubro de la empresa.

| Campo | Descripción |
|---|---|
| N | Número correlativo del registro. |
| Código de convocatoria | Identificador único de la convocatoria revisada en SEACE. |
| Título de la convocatoria | Nombre o denominación del proceso. |
| Entidad convocante | Institución pública responsable de la convocatoria. |
| Rubro o categoría | Sector o rubro asociado al giro de la empresa. |
| Fecha de publicación | Fecha y hora de publicación en SEACE. |
| Fecha de detección manual | Fecha y hora en que el personal identificó la convocatoria. |
| Tiempo de búsqueda manual (min) | Minutos empleados para ubicar y revisar la convocatoria. |
| Tiempo de desfase manual (h) | Horas entre publicación y detección manual. |
| Monto estimado (S/) | Valor económico estimado del proceso. |
| Estado de la convocatoria | Activa, cerrada, anulada u otro estado visible. |
| Total de campos revisados | Cantidad de campos contrastados con SEACE. |
| Errores u omisiones detectadas | Cantidad de campos incompletos, duplicados o inconsistentes. |
| Campos correctos | Campos revisados menos errores u omisiones. |
| Cobertura manual | Sí/No: indica si la convocatoria fue detectada por el método manual. |
| Responsable del registro | Personal que realizó la revisión manual. |
| Observaciones | Comentarios relevantes del proceso manual. |

## 3. Ficha de registro del postest: proceso automatizado

**Nombre del instrumento:** Ficha de registro del postest para el proceso automatizado mediante la plataforma web inteligente y scraping.

**Finalidad:** Registrar los datos obtenidos después de la implementación de la plataforma, con el propósito de medir la mejora respecto al proceso manual.

**Unidad de análisis:** Convocatoria estatal obtenida automáticamente desde el portal SEACE mediante scraping.

**Aplicación:** Sobre la misma muestra de 30 convocatorias filtradas según el rubro de la empresa.

| Campo | Descripción |
|---|---|
| N | Número correlativo del registro. |
| Código de convocatoria | Identificador único capturado por el sistema. |
| Título de la convocatoria | Nombre o denominación extraída automáticamente. |
| Entidad convocante | Institución pública obtenida por scraping. |
| Rubro o categoría | Clasificación asignada por el sistema. |
| Fecha de publicación | Fecha y hora obtenida desde SEACE. |
| Fecha de detección automática | Fecha y hora en que la plataforma registró la convocatoria. |
| Tiempo de extracción (seg) | Segundos requeridos por el scraping para obtener datos. |
| Tiempo de alerta (min) | Minutos transcurridos hasta mostrar o notificar la convocatoria relevante. |
| Monto estimado (S/) | Valor económico procesado por la plataforma. |
| Estado de la convocatoria | Activa, cerrada, anulada u otro estado capturado. |
| Total de campos extraídos | Cantidad de campos obtenidos por la plataforma. |
| Campos correctos | Campos que coinciden con la información real del portal SEACE. |
| Precisión de extracción (%) | Porcentaje de coincidencia entre datos extraídos y datos reales. |
| Cobertura automática | Sí/No: indica si el sistema capturó la convocatoria del rubro. |
| Responsable de validación | Persona que validó la extracción automática. |
| Observaciones | Comentarios sobre fallas, alertas o validaciones del sistema. |

## 4. Matriz de consolidación y comparación

| Indicador | Unidad | Objetivo específico | Medición pretest | Medición postest | Técnica de cálculo | Interpretación |
|---|---|---|---|---|---|---|
| Tiempo de búsqueda | min/seg | Reducir el tiempo de búsqueda de convocatorias estatales. | Promedio de minutos invertidos manualmente. | Promedio de segundos requeridos por el scraping. | ((Pretest - Postest en min) / Pretest) x 100 | Menor valor en postest indica mejora. |
| Precisión de datos | % | Mejorar la precisión de la información recopilada desde SEACE. | Campos correctos / total de campos revisados. | Campos correctos / total de campos extraídos. | Postest - Pretest | Mayor valor en postest indica mejora. |
| Tiempo de identificación | horas/min | Incrementar la oportunidad de identificación de convocatorias relevantes. | Promedio del desfase entre publicación y detección manual. | Promedio del tiempo entre publicación y alerta automática. | ((Pretest - Postest) / Pretest) x 100 | Menor valor en postest indica mejora. |
| Esfuerzo manual | horas-hombre | Reducir el esfuerzo manual en el monitoreo de convocatorias. | Horas dedicadas al monitoreo, filtrado y registro manual. | Horas requeridas para supervisar la ejecución automatizada. | Pretest - Postest y porcentaje de reducción. | Menor valor en postest indica mejora. |
| Cobertura de seguimiento | % | Mejorar la cobertura de seguimiento de convocatorias estatales. | Convocatorias detectadas manualmente / total de la muestra. | Convocatorias capturadas por el sistema / total de la muestra. | Postest - Pretest | Mayor valor en postest indica mejora. |

## 5. Procedimiento de aplicación

El procedimiento se desarrolla en dos etapas. En la primera etapa, correspondiente al pretest, el personal revisa manualmente las convocatorias seleccionadas en SEACE y registra cada campo de la ficha manual. En la segunda etapa, correspondiente al postest, se ejecuta la plataforma LicitIA para obtener convocatorias mediante scraping y se registran los resultados en la ficha automatizada. Finalmente, los datos se consolidan en la matriz comparativa para calcular diferencias absolutas, porcentajes de mejora, precisión y cobertura.

## 6. Criterios de cálculo

- Precisión de datos (%) = campos correctos / total de campos revisados o extraídos x 100.
- Cobertura de seguimiento (%) = convocatorias detectadas o capturadas / total de convocatorias de la muestra x 100.
- Reducción de tiempo o esfuerzo (%) = (valor pretest - valor postest) / valor pretest x 100.
- Incremento de precisión o cobertura (%) = (valor postest - valor pretest) / valor pretest x 100.

## 7. Validez y confiabilidad

La validez de contenido puede establecerse mediante juicio de expertos, evaluando la pertinencia, claridad, coherencia y suficiencia de los campos incluidos. La confiabilidad puede verificarse mediante una aplicación piloto y contraste de los registros con la información publicada en SEACE y con los datos generados por la plataforma.
