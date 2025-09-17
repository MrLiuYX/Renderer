# Renderer
Unity大规模渲染方案
所有Demo:
采用了DMII方式进行渲染
透出Handler进行控制
泛型扩展Data数据
Demo2
合批版:
  采用MeshCombiner合并Mesh 并存储单个Mesh的顶点范围
  数据贴图传输顶点位置进行控制与隐藏

扩展:
继承对应的渲染器所需的数据即可进行贴图数据扩展 
Shader传输可自定义
