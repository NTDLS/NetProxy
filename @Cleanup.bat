@Echo Off


attrib *.* -h -a -r -s

rd .vs /s /q

rd NetProxy.Client\obj /s /q
rd NetProxy.Client\bin /s /q
rd NetProxy.Client\.vs /s /q

rd NetProxy.Library\obj /s /q
rd NetProxy.Library\bin /s /q
rd NetProxy.Library\.vs /s /q

rd NetProxy.Hub\obj /s /q
rd NetProxy.Hub\bin /s /q
rd NetProxy.Hub\.vs /s /q

rd NetProxy.Service\obj /s /q
rd NetProxy.Service\bin /s /q
rd NetProxy.Service\.vs /s /q

rd packages /s /q
rd setup\Output /s /q

