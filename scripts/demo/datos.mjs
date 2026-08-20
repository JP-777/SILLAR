/**
 * Los datos con los que se enseña SILLAR.
 *
 * **No hay ningún negocio real aquí.** Ni nombre, ni dirección, ni teléfono:
 * este repositorio contiene el producto, nunca a un cliente (ADR-008). Lo que
 * hay son productos creíbles de una librería y bazar cualquiera, con los
 * nombres escritos según el diccionario del proyecto:
 *
 *     Producto + característica principal + marca/modelo + presentación
 *
 * Y a propósito **no todos son iguales**: hay tres sin foto, dos sin precio y
 * tres con varias presentaciones. Un catálogo donde todo está completo no
 * enseña nada de lo que el sistema sabe hacer.
 */

/** Las marcas. El color es solo para la imagen que se genera, no viaja al API. */
export const MARCAS = [
  { name: 'Stanford', description: 'Cuadernos y papelería escolar.', color: '#1E5AA8' },
  { name: 'Artesco', description: 'Útiles de escritorio y arte.', color: '#C8102E' },
  { name: 'Faber-Castell', description: 'Lápices, colores y trazo técnico.', color: '#0F7B3E' },
  { name: 'Vinifan', description: 'Forros, micas y archivadores.', color: '#6B4E9B' },
];

/**
 * Dos niveles, porque el árbol es lo que distingue a una categoría de una
 * etiqueta. `parent` es el nombre de la de arriba.
 */
export const CATEGORIAS = [
  { name: 'Papelería', parent: null, color: '#8A6D3B' },
  { name: 'Cuadernos', parent: 'Papelería', color: '#1E5AA8' },
  { name: 'Hojas y blocs', parent: 'Papelería', color: '#4A7C9B' },
  { name: 'Escritura', parent: null, color: '#C8102E' },
  { name: 'Lápices y colores', parent: 'Escritura', color: '#D97706' },
  { name: 'Plumones y resaltadores', parent: 'Escritura', color: '#9333EA' },
  { name: 'Oficina', parent: null, color: '#475569' },
  { name: 'Archivo', parent: 'Oficina', color: '#6B4E9B' },
  { name: 'Arte', parent: null, color: '#0F7B3E' },
];

/**
 * Veinte productos.
 *
 * - `precio: null` es **a consultar**, que no es lo mismo que gratis.
 * - `foto: false` deja ver el cuadrado con el nombre, que es una decisión de
 *   diseño y no un hueco.
 * - `presentaciones` convierte al producto en el caso que distingue a SILLAR
 *   de una lista: un nombre, varias cosas que se venden por separado. Y una
 *   de ellas cuesta distinto, para que la tarjeta diga «Desde».
 */
export const PRODUCTOS = [
  {
    name: 'Cuaderno universitario cuadriculado Stanford A4 100 hojas',
    corta: 'Tapa dura, 100 hojas cuadriculadas.',
    descripcion:
      'Cuaderno de tapa dura con espiral doble. Hoja de 80 gramos y cuadrícula de 5 milímetros. El formato A4 entra en cualquier archivador.',
    marca: 'Stanford',
    categorias: ['Cuadernos'],
    precio: 12.5,
    unidad: 'Por unidad',
    codigo: 'STF-CU-A4-100',
    color: '#1E5AA8',
  },
  {
    name: 'Cuaderno universitario rayado Stanford A4 100 hojas',
    corta: 'El mismo cuaderno, con hoja rayada.',
    marca: 'Stanford',
    categorias: ['Cuadernos'],
    precio: 12.5,
    codigo: 'STF-RA-A4-100',
    color: '#2E6AB8',
  },
  {
    name: 'Cuaderno cuadriculado Stanford A5 80 hojas',
    corta: 'Formato pequeño, cabe en el bolso.',
    marca: 'Stanford',
    categorias: ['Cuadernos'],
    precio: 8.9,
    codigo: 'STF-CU-A5-80',
    color: '#3E7AC8',
  },
  {
    name: 'Papel bond Report A4 75 gramos paquete 500 hojas',
    corta: 'Resma para impresora y fotocopiadora.',
    categorias: ['Hojas y blocs'],
    precio: 24,
    unidad: 'Por paquete',
    codigo: 'PB-A4-75-500',
    color: '#4A7C9B',
  },
  {
    name: 'Block de dibujo Artesco A3 20 hojas',
    corta: 'Hoja gruesa: no traspasa el marcador.',
    marca: 'Artesco',
    categorias: ['Hojas y blocs', 'Arte'],
    precio: 11,
    codigo: 'ART-BD-A3',
    color: '#0F7B3E',
  },
  {
    name: 'Papel lustre surtido paquete 10 hojas',
    corta: 'Diez colores, para trabajos manuales.',
    categorias: ['Arte', 'Hojas y blocs'],
    precio: 3.5,
    codigo: 'PL-SUR-10',
    foto: false,
  },
  {
    // El caso del plumón, que es el que distingue a SILLAR de una lista.
    name: 'Plumón para pizarra acrílica Artesco recargable',
    corta: 'Se borra en seco. Tres colores, cada uno con su código.',
    descripcion:
      'Plumón de punta redonda para pizarra acrílica. Recargable: la tinta se vende aparte, así que el cuerpo dura años.',
    marca: 'Artesco',
    categorias: ['Plumones y resaltadores'],
    precio: 4.5,
    variantLabel: 'Color',
    color: '#C8102E',
    presentaciones: [
      { valor: 'Negro', codigo: 'ART-PZ-NEG', barras: '7751271000018', precio: null },
      { valor: 'Rojo', codigo: 'ART-PZ-ROJ', barras: '7751271000025', precio: null },
      { valor: 'Azul metálico', codigo: 'ART-PZ-AZU', barras: '7751271000032', precio: 5.9 },
    ],
  },
  {
    name: 'Resaltador fluorescente Artesco punta biselada',
    corta: 'Punta biselada: subraya fino o ancho.',
    marca: 'Artesco',
    categorias: ['Plumones y resaltadores'],
    precio: 2.8,
    variantLabel: 'Color',
    color: '#D97706',
    presentaciones: [
      { valor: 'Amarillo', codigo: 'ART-RE-AMA', barras: '7751271000049', precio: null },
      { valor: 'Verde', codigo: 'ART-RE-VER', barras: '7751271000056', precio: null },
    ],
  },
  {
    name: 'Lápiz de grafito Faber-Castell 2B caja 12 unidades',
    corta: 'Trazo blando, para dibujo y escritura.',
    marca: 'Faber-Castell',
    categorias: ['Lápices y colores'],
    precio: 15,
    unidad: 'Por caja',
    codigo: 'FC-GR-2B-12',
    color: '#0F7B3E',
  },
  {
    name: 'Colores de madera Faber-Castell caja 24 largos',
    corta: 'Veinticuatro colores, mina resistente.',
    marca: 'Faber-Castell',
    categorias: ['Lápices y colores', 'Arte'],
    precio: 32.9,
    unidad: 'Por caja',
    codigo: 'FC-CO-24',
    color: '#1F8B4E',
  },
  {
    name: 'Borrador de nata Faber-Castell mediano',
    corta: 'No mancha ni deja residuo duro.',
    marca: 'Faber-Castell',
    categorias: ['Lápices y colores'],
    precio: 1.5,
    codigo: 'FC-BO-MED',
    color: '#2F9B5E',
  },
  {
    name: 'Tajador metálico de dos entradas',
    corta: 'Cuchilla de acero, dos medidas.',
    categorias: ['Lápices y colores'],
    precio: 2.2,
    codigo: 'TJ-MET-2',
    foto: false,
  },
  {
    name: 'Archivador de palanca Vinifan lomo ancho oficio',
    corta: 'Lomo ancho, con rótulo cambiable.',
    descripcion:
      'Archivador de palanca con refuerzo metálico en el lomo y compresor interno. Tamaño oficio.',
    marca: 'Vinifan',
    categorias: ['Archivo'],
    precio: 14.5,
    variantLabel: 'Color',
    color: '#6B4E9B',
    presentaciones: [
      { valor: 'Negro', codigo: 'VF-AP-NEG', barras: '7750182000015', precio: null },
      { valor: 'Azul', codigo: 'VF-AP-AZU', barras: '7750182000022', precio: null },
      { valor: 'Rojo', codigo: 'VF-AP-ROJ', barras: '7750182000039', precio: 16.9 },
    ],
  },
  {
    name: 'Mica portapapeles Vinifan A4 paquete 100 unidades',
    corta: 'Perforadas: entran en cualquier archivador.',
    marca: 'Vinifan',
    categorias: ['Archivo'],
    precio: 19.9,
    unidad: 'Por paquete',
    codigo: 'VF-MI-A4-100',
    color: '#7B5EAB',
  },
  {
    name: 'Forro plástico Vinifan para cuaderno A4',
    corta: 'Transparente, con solapa.',
    marca: 'Vinifan',
    categorias: ['Archivo', 'Papelería'],
    precio: 1.8,
    codigo: 'VF-FO-A4',
    color: '#8B6EBB',
  },
  {
    name: 'Engrapador metálico de escritorio 26/6',
    corta: 'Hasta 20 hojas. Base antideslizante.',
    categorias: ['Oficina'],
    precio: 26,
    codigo: 'EN-MET-266',
    color: '#475569',
  },
  {
    name: 'Perforador de dos huecos metálico',
    corta: 'Guía milimetrada y depósito extraíble.',
    categorias: ['Oficina'],
    precio: 21.5,
    codigo: 'PF-2H-MET',
    color: '#576579',
  },
  {
    name: 'Témpera escolar Artesco frasco 250 mililitros',
    corta: 'Lavable, de secado rápido.',
    marca: 'Artesco',
    categorias: ['Arte'],
    precio: 6.5,
    unidad: 'Por frasco',
    codigo: 'ART-TE-250',
    color: '#E11D48',
  },
  {
    // A consultar: el precio depende del trabajo, y decirlo es más honesto
    // que inventar un número.
    name: 'Anillado espiral por documento hasta 100 hojas',
    corta: 'Se cotiza según el grosor y la tapa.',
    descripcion:
      'Anillado con espiral plástico y tapa transparente. El precio depende del número de hojas y del tipo de tapa.',
    categorias: ['Oficina'],
    precio: null,
    unidad: 'Por documento',
    color: '#334155',
  },
  {
    name: 'Impresión láser blanco y negro por hoja A4',
    corta: 'Se cotiza por volumen.',
    categorias: ['Oficina'],
    precio: null,
    foto: false,
  },
];
